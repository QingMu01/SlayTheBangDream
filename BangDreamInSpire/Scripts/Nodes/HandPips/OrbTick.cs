using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    public static void Tick(Player player, NEnergyCounter counter, Texture2D capturedEnergy, float delta)
    {
        if (!_tickLogged)
        {
            _tickLogged = true;
            GD.Print($"[BangDreamInSpire/handpips] tick first: player={(player != null)} mode={Config.Data.Mode}");
        }

        if (player == null)
        {
            return;
        }

        bool snap = _snap;
        _blockUsed = 0; // Block 池按帧复用
        Config.FlushIfDirty(); // 设置页去抖落盘
        _snap = false;

        NPlayerHand hand = NPlayerHand.Instance;
        bool drawEnergy = Config.Data.Mode == OrbDisplayMode.All || Config.Data.Mode == OrbDisplayMode.EnergyOnly;
        bool drawSecondary = Config.Data.Mode == OrbDisplayMode.All || Config.Data.Mode == OrbDisplayMode.SecondaryOnly;

        if ((!drawEnergy && !drawSecondary) || hand == null || !GodotObject.IsInstanceValid(hand))
        {
            Hide(player);
            return;
        }

        PlayerCombatState state = player.PlayerCombatState;
        if (state == null)
        {
            Hide(player);
            return;
        }

        IReadOnlyList<NHandCardHolder> holders = hand.ActiveHolders;

        Overlay overlay = OverlayFor(player, hand);
        if (Config.Data.Debug && !_layoutLogged)
        {
            _layoutLogged = true;
            GD.Print($"[BangDreamInSpire/handpips] layout: holders={holders.Count} energy={state.Energy}/{state.MaxEnergy} " +
                     $"tex={(overlay?.Energy.Texture?.ResourcePath ?? "none")}");
        }

        if (overlay == null)
        {
            return;
        }

        // 同帧去重：同一个渲染帧里 Tick 可能被调两次（_Process + _Ready 的 CallDeferred），
        // 第二次会把 overlay.Time 再推一次，表现为整排动画双倍速。同帧第二次进入直接返回。
        ulong frame = Engine.GetProcessFrames();
        if (overlay.LastTickFrame == frame)
        {
            return;
        }

        overlay.LastTickFrame = frame;

        LingeredPrecognition.Step(delta);

        // 手牌收起（回合结束 / 敌方回合）：球保留、变暗、平滑下沉；动画照常播。
        // 手牌消失满 60 ms 才算收起 —— 受击/动画期间 holder 的瞬时抖动不会打断。
        if (holders.Count == 0)
        {
            overlay.NoHandT += delta;
        }
        else
        {
            overlay.NoHandT = 0f;
        }

        bool resting = overlay.NoHandT > 0.06f;

        if (snap)
        {
            overlay.Placed = false;
            overlay.Dragging = false;
            overlay.DragLatch = false;
        }

        int energy = Math.Clamp(state.Energy, 0, MaxPips);
        int maxEnergy = Math.Clamp(state.MaxEnergy, 0, MaxPips);

        if (drawEnergy && overlay.Energy.Texture == null)
        {
            overlay.Energy.Texture = ResolveEnergyTexture(player, capturedEnergy, counter);
            if (overlay.Energy.Texture != null)
            {
                bool ok = TryDominantColor(overlay.Energy.Texture, out Color color);
                overlay.Energy.Glow = ok ? color : FallbackGlow;
                overlay.Energy.NeedsSample = !ok;
            }
        }

        // 容器可能在这一帧还没挂上/已被释放：读它之前必须守卫，否则异常会被全局 catch 吞掉
        Control handContainerNode = hand.CardHolderContainer as Control;
        Vector2 handCentre = handContainerNode != null && GodotObject.IsInstanceValid(handContainerNode)
            ? handContainerNode.GlobalPosition
            : new Vector2(overlay.HaveLast ? overlay.LastFocusX : 0f, 0f);

        // 手牌刚飞回来（0 → 有牌）的头 0.2s 算"还没到手"：这段时间不亮、不播获得动画
        double nowTick = Time.GetTicksMsec() / 1000.0;
        if (overlay.LastHolderCount == 0 && holders.Count > 0)
        {
            overlay.SettleAt = nowTick;
        }

        bool settling = holders.Count > 0 && nowTick < overlay.SettleAt + 0.20;

        float focusX = handCentre.X;
        NHandCardHolder focused0 = hand.FocusedHolder;
        if (focused0 != null && GodotObject.IsInstanceValid(focused0) && focused0.IsVisibleInTree())
        {
            focusX = focused0.GlobalPosition.X;
        }

        bool headLifted = false;
        float cardsTop = float.MaxValue;

        // 拖牌瞄准时以被拖的那张为锚点：球排跟着它走
        NHandCardHolder dragging = hand.InCardPlay ? CurrentCardHolder(hand) : null;
        // 一帧只认一张焦点牌：预览消耗 / 预测 ghost / 后续布局全部复用这一个值，
        // 避免同一帧里两次反射各自读到不同结果（帧内切换卡片时会出现"费用算 A、ghost 算 B"）
        NHandCardHolder focusHolder = dragging ?? hand.FocusedHolder;

        // 照原版：遍历 CardHolderContainer 的子节点，只统计"已经落位"的卡。
        // 飞入/缩放中的卡不参与，手牌顶边才不会跟着跳。
        Node cardContainer = hand.CardHolderContainer;
        if (cardContainer != null && GodotObject.IsInstanceValid(cardContainer))
        {
            int childCount = cardContainer.GetChildCount(false);
            for (int i = 0; i < childCount; i++)
            {
                Node child = cardContainer.GetChild(i, false);
                if (child is not NHandCardHolder holder)
                {
                    continue;
                }

                if (!GodotObject.IsInstanceValid(holder) || !holder.IsVisibleInTree() || holder == dragging)
                {
                    continue;
                }

                if (!Landed(holder, hand.FocusedHolder))
                {
                    continue;
                }

                float top;
                try
                {
                    top = holder.Hitbox.GetGlobalRect().Position.Y;
                }
                catch
                {
                    top = holder.GetGlobalRect().Position.Y;
                }

                if (top < cardsTop)
                {
                    cardsTop = top;
                }

                if (!Config.Data.AvoidHead)
                {
                    continue;
                }

                try
                {
                    TextureRect head = FindHeadCached(holder);
                    if (head != null && GodotObject.IsInstanceValid(head) && head.IsVisibleInTree())
                    {
                        Rect2 headRect = head.GetGlobalRect();
                        float progress;
                        try
                        {
                            progress = head.GetInstanceShaderParameter(P_Progress).AsSingle();
                        }
                        catch
                        {
                            progress = -1f;
                        }

                        bool peeked = progress >= 0f ? progress > 0.6f : headRect.Position.Y < top - 4f;
                        if (peeked && Mathf.Abs(headRect.GetCenter().X - focusX) < 260f
                            && headRect.Position.Y > 0f && headRect.Position.Y + 8f < cardsTop)
                        {
                            cardsTop = headRect.Position.Y - 8f;
                            headLifted = true;
                        }
                    }
                }
                catch
                {
                    // 拿不到头就用卡顶
                }
            }
        }

        float rowOffsetY = Mathf.Clamp(Config.Data.RowOffsetY, 0f, 160f);

        if (cardsTop == float.MaxValue)
        {
            // 自由摆放：位置来自 FreeX/FreeY，没有卡（回合结束/敌方回合/手牌未落位）时
            // 保留上一帧继续显示；只有"从来没建立过位置"才隐藏。
            bool freeReady = Config.Data.FreePlace
                             && (overlay.HaveLast || overlay.FreeSeeded
                                 || Config.Data.FreeX > 0.5f || Config.Data.FreeY > 0.5f);
            if (!overlay.HaveLast && !freeReady)
            {
                Hide(player);
                return;
            }

            cardsTop = overlay.LastCardsTop;
        }

        NHandCardHolder focused = focusHolder;
        if (focused != null && GodotObject.IsInstanceValid(focused) && focused.IsVisibleInTree())
        {
            focusX = focused.GlobalPosition.X;
            if (Config.Data.AvoidHead)
            {
                cardsTop = HeadAwareCardsTop(hand, holders, cardsTop, focusX);
            }
        }

        float targetY = cardsTop - rowOffsetY;

        // 原版同款 Holding 锁：拖牌期间只允许往上抬，不被下拽
        if (dragging != null && GodotObject.IsInstanceValid(dragging))
        {
            float dragTop;
            try
            {
                dragTop = dragging.Hitbox.GetGlobalRect().Position.Y;
            }
            catch
            {
                dragTop = dragging.GetGlobalRect().Position.Y;
            }

            float dragTarget = dragTop - rowOffsetY;
            overlay.HoldY = Mathf.Min(overlay.HoldY ?? overlay.RowY ?? dragTarget, dragTarget);
            targetY = overlay.HoldY.Value;
            targetY = Mathf.Max(targetY, 8f + rowOffsetY * 0.5f);
        }
        else
        {
            overlay.HoldY = null;
        }

        overlay.LastCardsTop = cardsTop;
        overlay.LastFocusX = focusX;
        overlay.HaveLast = true;

        int previewCost = Config.Data.PreviewEffects ? PreviewCost(player, focusHolder) : 0;

        // 能量一变化（到账/消耗）就是"真实结果落地"：预测 ghost 必须在当帧清空，不留尾巴
        bool energyArrived = overlay.RawEnergy >= 0 && overlay.RawEnergy != energy;
        overlay.RawEnergy = energy;

        if (Config.Data.PreviewEffects)
        {
            UpdateIncoming(overlay, focusHolder, player);
        }
        else
        {
            ClearIncoming(overlay);
        }

        if (energyArrived)
        {
            SetIncoming(overlay.Energy, 0);
        }

        // ghost 是"顶替暗格"，不是额外插一颗：总数只在对不上暗格时才长
        int energyBase = Math.Max(Math.Max(maxEnergy, energy), energy + Math.Max(0, previewCost - energy));
        int energyTotal = drawEnergy
            ? Math.Clamp(Math.Max(energyBase, energy + overlay.Energy.Incoming), 0, MaxPips)
            : 0;

        UpdateSecondaries(overlay, player, state);

        // ── 主能量块 ──
        Block blockE = ComputeBlock(energyTotal, Config.Data.OrbPixels, 1f);
        float halfWidth = blockE.Width * 0.5f;
        float margin = halfWidth + 46f;
        float viewportWidth = overlay.Root.GetViewportRect().Size.X;

        float anchorX = focusX;
        if (drawEnergy)
        {
            if (Config.Data.Align == RowAlign.Left)
            {
                anchorX = LeftmostCardEdge(holders) + halfWidth;
            }
            else if (Config.Data.Align == RowAlign.Right)
            {
                anchorX = RightmostCardEdge(holders) - halfWidth;
            }
        }

        float x = Mathf.Clamp(anchorX, margin, Mathf.Max(margin, viewportWidth - margin));

        // ── 副资源块：各自独立换行，逐块堆在主能量块上方，不混行 ──
        var secBlocks = SecBlocksBuf;
        secBlocks.Clear();
        float cursor = -blockE.Height;
        foreach (Row row in overlay.Secondary.Values)
        {
            // 副资源同理：ghost 顶掉暗格，不额外加位置
            int secTotal = Math.Clamp(Math.Max(row.DisplayTotal, row.Amount + row.Incoming), 0, MaxPips);
            Block blk = ComputeBlock(secTotal, Config.Data.SecondaryPixels, SubRowScale);
            // 副资源块与上一块之间：沿用原版 38 px，别让两排球压在一起
            cursor -= 38f * Mathf.Clamp(Config.Data.SecondaryPixels / 46f, 0.62f, 1.4f);
            secBlocks.Add((row, blk, cursor, secTotal));
            cursor -= blk.Height;
        }

        float blockTop = cursor;

        static float defaultTopBelowPotions(Vector2 view) => Mathf.Round(Mathf.Max(150f, view.Y * 0.16f));

        Vector2 target;
        if (Config.Data.FreePlace)
        {
            // Root 是 hand（NPlayerHand）的子节点，而手牌自己在动：
            //   AnimDisable / AnimEnable 把 position 在 (0,0)↔(0,100) 之间补 0.2 s，
            //   AnimIn / AnimOut 补到 (0,500)（0.8 s）。补间是节点 _Process 之后才落到父节点上的，
            //   所以只在 Tick 里写 GlobalPosition = 事后纠正：这一帧写进去的坐标立刻又被父变换推走，
            //   下一帧再拉回来 —— 整排就是一帧来一帧去的上下抖。
            // TopLevel = 这一排的变换不再继承父节点（Position 即画布坐标），父节点怎么动都带不走它。
            // 只影响 transform：z 序仍按 z_index + 树顺序，悬停那张卡（ZIndex=1）照样盖住球。
            if (!overlay.Root.TopLevel)
            {
                overlay.Root.TopLevel = true;
            }

            if (!overlay.FreeSeeded)
            {
                // 父链累积缩放只取一次：之后大小完全由 FreeScale 决定（父缩放不再逐帧参与），
                // 但首次开启时视觉尺寸和开关前一致。
                overlay.FreeParentScale = ParentGlobalScale(overlay.Root);

                if (!Config.Data.FreePlaceInitialized || (Config.Data.FreeX <= 0.5f && Config.Data.FreeY <= 0.5f))
                {
                    // 默认位置：屏幕水平居中，药水栏下方一点（顶部条高度约 120px）
                    var view = overlay.Root.GetViewportRect().Size;
                    Config.Data.FreeX = Mathf.Round(view.X * 0.5f);
                    Config.Data.FreeY = defaultTopBelowPotions(view);
                    Config.Data.FreePlaceInitialized = true;
                    Config.Save();
                }

                // 范围锁定：任何一次进入自由模式都把位置夹回窗口内（含边距）
                Config.Data.FreeX = Mathf.Clamp(Config.Data.FreeX, 24f, Mathf.Max(24f, overlay.Root.GetViewportRect().Size.X - 24f));
                Config.Data.FreeY = Mathf.Clamp(Config.Data.FreeY, 72f, Mathf.Max(72f, overlay.Root.GetViewportRect().Size.Y - 72f));

                overlay.FreeSeeded = true;
            }

            // 变换框先把这一帧的拖角缩放写回配置，再统一应用，避免拖角时慢一帧
            UpdateGrip(overlay, blockE, delta);

            target = new Vector2(Config.Data.FreeX, Config.Data.FreeY);
            target = DragIfGrabbed(overlay, target, halfWidth, blockTop);
            // TopLevel 之后 Position 就是画布坐标（不再经父变换反算），所以直接写 Position
            overlay.Root.Scale = Vector2.One * (Mathf.Clamp(Config.Data.FreeScale, 0.5f, 2.5f)
                                               / Mathf.Max(0.05f, overlay.FreeParentScale));
            overlay.RowX = target.X;
            overlay.RowY = target.Y;
            overlay.Placed = true;
            overlay.Root.Position = target;
        }
        else
        {
            overlay.FreeSeeded = false;
            overlay.Dragging = false;
            overlay.DragLatch = false;
            // 回到跟手牌模式：必须恢复父子变换继承，下面的 GlobalPosition 才回到旧语义
            if (overlay.Root.TopLevel)
            {
                overlay.Root.TopLevel = false;
            }

            overlay.Root.Scale = Vector2.One;
            HideGrip(overlay);

            float top = targetY + blockTop;
            if (top < 8f)
            {
                targetY += 8f - top;
            }

            if (!overlay.Placed)
            {
                overlay.RowX = x;
                overlay.RowY = targetY;
                overlay.Placed = true;
            }
            else
            {
                // 原版同款：位置是"状态量"，每帧只向目标指数收敛（Y 40/s、X 24/s）。
                // 不再从节点的 GlobalPosition 反读，外部位移（抖动）不会反馈进状态。
                overlay.RowX = Mathf.Lerp(overlay.RowX ?? x, x, 1f - Mathf.Exp(-delta * 24f));
                overlay.RowY = Mathf.Lerp(overlay.RowY ?? targetY, targetY, 1f - Mathf.Exp(-delta * 40f));
            }

            overlay.Root.GlobalPosition = new Vector2(overlay.RowX ?? x, overlay.RowY ?? targetY);
        }

        // 出牌后消失、下次选中淡入：只改透明度，不打断任何更新
        // 自由摆放不参与：否则"出牌淡到全透明"会顶掉回合结束的压暗（YG 要的压暗必须看得见）
        double nowHide = Time.GetTicksMsec() / 1000.0;
        if (Config.Data.HideAfterPlay && !Config.Data.FreePlace)
        {
            // 触发条件用「能量下降」（打牌必然扣费），比数手牌可靠
            if (overlay.LastEnergy > 0 && energy < overlay.LastEnergy)
            {
                overlay.HideScheduled = true;
                overlay.HideStart = nowHide + 0.5;
            }

            if (overlay.HideScheduled && nowHide >= overlay.HideStart)
            {
                overlay.HiddenAfterPlay = true;
            }

            if ((dragging != null || hand.FocusedHolder != null)
                && overlay.HideScheduled && nowHide > overlay.HideStart + 0.25)
            {
                overlay.HideScheduled = false;
                overlay.HiddenAfterPlay = false;
            }
        }
        else
        {
            overlay.HideScheduled = false;
            overlay.HiddenAfterPlay = false;
        }

        // 自由摆放：resting/settling 只许影响压暗，不许复位"出牌淡出"（那是用户自己的设置）
        if ((resting || settling) && !Config.Data.FreePlace)
        {
            overlay.HideScheduled = false;
            overlay.HiddenAfterPlay = false;
        }

        overlay.Opacity = Mathf.MoveToward(overlay.Opacity, overlay.HiddenAfterPlay ? 0f : 1f, delta / 0.18f);
        // 压暗在自由摆放里也照常生效（YG 明确要）：只有位置/缩放脱钩，亮度不脱钩
        float dimTarget = resting ? 0.5f : (settling ? 0.62f : 1f);
        overlay.Dim = Mathf.MoveToward(overlay.Dim, dimTarget, delta / 0.25f);
        overlay.Root.Modulate = new Color(overlay.Dim, overlay.Dim, overlay.Dim, overlay.Opacity);

        if (Config.Data.FreePlace && overlay.GripRoot != null && GodotObject.IsInstanceValid(overlay.GripRoot))
        {
            // 变换框是给鼠标抓的 UI：按 Dim 反向补回来，回合结束压暗时也不会淡到抓不到
            float lift = Mathf.Clamp(1f / Mathf.Max(0.35f, overlay.Dim), 1f, 2f);
            overlay.GripRoot.Modulate = new Color(lift, lift, lift, lift);
        }
        overlay.Root.Visible = overlay.Opacity > 0.02f;
        overlay.Time += delta;
        if (overlay.Time > 3600f)
        {
            overlay.Time -= 3600f; // float 精度保护：长战斗也不会让动画相位抖动
        }

        // 手牌变多（抽牌/回手）：也把淡出状态复位，别让球一直藏着
        if (overlay.LastHolderCount >= 0 && holders.Count > overlay.LastHolderCount)
        {
            overlay.HideScheduled = false;
            overlay.HiddenAfterPlay = false;
        }

        overlay.LastHolderCount = holders.Count;

        // 回合刷新：手牌重新出现 或 回合数变化（手牌不一定清空，靠回合数更可靠）
        bool turnChanged = overlay.LastTurn != state.TurnNumber;
        overlay.LastTurn = state.TurnNumber;
        bool justRefreshed = overlay.WasHidden || turnChanged;

        if (justRefreshed)
        {
            overlay.WasHidden = false;
            // 跨回合必须复位"出牌淡出"：否则打出一张牌后整排淡到 0，
            // 新回合手牌已经换了却还停在全透明，看起来就是"能量球跨回合消失"。
            overlay.HideScheduled = false;
            overlay.HiddenAfterPlay = false;
            // 跨回合不重播整排：球一直在，只有数量变化的那几颗自己播动画（对齐原版）
        }

        // 手牌还在飞入 / 整排还暗着的时候不播获得动画，等它到位、亮起来再播
        bool handSettled = !resting && !settling;

        // 能量变多：只让"这次新点亮"的那几颗播刷新动画（波纹 + 边缘闪 + 星芒）
        if (handSettled && overlay.LastEnergy >= 0 && energy > overlay.LastEnergy)
        {
            overlay.Energy.SparkRequest = true;
            overlay.Energy.GainStart = overlay.LastEnergy;
            // 只让"这次新点亮"的那几颗播波纹（能量增加不一定新增球，可能是点亮已有球）
            for (int i = Mathf.Max(0, overlay.LastEnergy); i < energy && i < overlay.Energy.Pips.Count; i++)
            {
                overlay.Energy.Pips[i].RippleT = -0.12f;
                overlay.Energy.Pips[i].Flash = 1f;
                overlay.Energy.Pips[i].WhiteHit = 1f;
                overlay.Energy.Pips[i].PlusT = 1f;
                overlay.Energy.Pips[i].Gain = 1f;
            }
        }

        if (handSettled)
        {
            overlay.LastEnergy = energy;
        }

        // 自由摆放：位置完全固定，探头拱形（会把中间的球抬起来）也不参与
        overlay.Energy.Fan = headLifted && !Config.Data.FreePlace ? 1f : 0f;
        overlay.Energy.Root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(Config.Data.EnergyOpacity, 0.05f, 1f));
        PaintRow(overlay, overlay.Energy, energyTotal, energy, 0f, blockE, 0f, delta, previewCost);

        float secAlpha = Mathf.Clamp(Config.Data.SecondaryOpacity, 0.05f, 1f);

        // 副资源预扣价要反射 RitsuLib 的支付计划：每帧调太贵，0.2 s 一次就够
        double nowCost = Time.GetTicksMsec() / 1000.0;
        bool refreshCost = nowCost - overlay.SecCostAt > 0.2;
        if (refreshCost)
        {
            overlay.SecCostAt = nowCost;
        }

        CardModel hoveredCard = refreshCost ? focusHolder?.CardModel : null;
        foreach ((Row row, Block blk, float secY, int secTotalCount) in secBlocks)
        {
            // DisplayTotal 的 owner 只有一个（UpdateSecondaries 里按 amount/slots 算），这里不再覆盖
            if (row.DisplayTotal < row.Amount)
            {
                row.DisplayTotal = row.Amount;
            }

            int secCost = 0;
            if (Config.Data.PreviewEffects)
            {
                secCost = refreshCost
                    ? SecondaryBridge.Cost(hoveredCard, row.Key)
                    : row.PreviewCostCache;
                row.PreviewCostCache = secCost;


            }

            row.Root.Modulate = new Color(1f, 1f, 1f, secAlpha);
            PaintRow(overlay, row, secTotalCount, row.Amount, secY, blk,
                AlignOffset(blockE.Width, blk.Width), delta, secCost);
        }

        if (overlay.Energy.NeedsSample && overlay.Energy.Pips.Count > 0)
        {
            SampleFromScreen(overlay.Energy);
        }

        if (Config.Data.Debug)
        {
            TickProbe(overlay);
            TickFxLog(overlay, player, blockE, energy, maxEnergy, previewCost);
        }
    }
}
