using System;
using Godot;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// 游戏内「模组配置 → 更直观的手牌 · 精简版」页。走 RitsuLib 的 ModSettings，
/// 每个开关/滑杆直接读写 config.json，改完立刻生效（不用重启）。
/// </summary>
internal static class SettingsPage
{
    /// <summary>
    /// 主菜单 / 地图上没有 Tick 在跑，去抖的配置会一直不落盘（退出即丢）。
    /// 用 SceneTree 计时器兜底：0.3 s 后强制写一次。
    /// </summary>
    private static void ScheduleFlush()
    {
        try
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                SceneTreeTimer timer = tree.CreateTimer(0.3);
                if (timer != null)
                {
                    timer.Timeout += Config.FlushDeferredNow;
                }
            }
        }
        catch
        {
            // 计时器拿不到就退回 Tick 里的 FlushIfDirty
        }
    }

    public sealed class Binding<T> : IModSettingsValueBinding<T>
    {
        private readonly Func<T> _read;
        private readonly Action<T> _write;

        public Binding(string key, Func<T> read, Action<T> write)
        {
            DataKey = key;
            _read = read;
            _write = write;
        }

        public string ModId => ModEntry.ModId;

        public string DataKey { get; }

        public SaveScope Scope => SaveScope.Global;

        public T Read() => _read();

        public void Write(T value)
        {
            _write(value);
            Config.Data.Sanitize();
            Config.SaveDeferred();
            ScheduleFlush();
            ModEntry.ApplyLive();
        }

        public void Save() => Config.Save();
    }

    /// <summary>浮点设置项：设置页滑杆只认 double。</summary>
    private sealed class FloatBinding : IModSettingsValueBinding<double>
    {
        private readonly Func<float> _read;
        private readonly Action<float> _write;

        public FloatBinding(string key, Func<float> read, Action<float> write)
        {
            DataKey = key;
            _read = read;
            _write = write;
        }

        public string ModId => ModEntry.ModId;

        public string DataKey { get; }

        public SaveScope Scope => SaveScope.Global;

        public double Read() => _read();

        public void Write(double value)
        {
            _write((float)value);
            Config.Data.Sanitize();
            Config.SaveDeferred();
            ScheduleFlush();
            ModEntry.ApplyLive();
        }

        public void Save() => Config.Save();
    }

    private static Binding<bool> Toggle(string key, Func<bool> get, Action<bool> set) => new(key, get, set);

    private static Binding<int> Int(string key, Func<int> get, Action<int> set) => new(key, get, set);

    /// <summary>把「手牌资源显示」这一组控件追加到 BangDreamInSpire 已有的设置页上。</summary>
    public static void AppendTo(ModSettingsPageBuilder page)
    {
        try
        {
            // 设置页注册早于模组初始化：这里先确保配置已加载，免得界面读到默认值再被覆盖。
            Config.Load(ModEntry.ModDir);
            {
                page
                    .AddSection("hand_pips_display", section =>
                    {
                        section.WithTitle(ModSettingsText.Literal("显示"));

                        section.AddChoice("mode",
                            ModSettingsText.Literal("显示哪几排"),
                            new Binding<OrbDisplayMode>("mode", () => Config.Data.Mode, v => Config.Data.Mode = v),
                            new[]
                            {
                                new ModSettingsChoiceOption<OrbDisplayMode>(OrbDisplayMode.All, ModSettingsText.Literal("全都有（主 + 副）")),
                                new ModSettingsChoiceOption<OrbDisplayMode>(OrbDisplayMode.None, ModSettingsText.Literal("全都没有")),
                                new ModSettingsChoiceOption<OrbDisplayMode>(OrbDisplayMode.EnergyOnly, ModSettingsText.Literal("只有主能量")),
                                new ModSettingsChoiceOption<OrbDisplayMode>(OrbDisplayMode.SecondaryOnly, ModSettingsText.Literal("只有副能量")),
                            },
                            ModSettingsText.Literal("一个按钮切四种状态，下一帧生效。"),
                            ModSettingsChoicePresentation.Stepper);
                    })
                    .AddSection("hand_pips_effects", section =>
                    {
                        section.WithTitle(ModSettingsText.Literal("特效开关"));

                        section.AddToggle("idle_bob",
                            ModSettingsText.Literal("待机浮动"),
                            Toggle("idle_bob", () => Config.Data.IdleBob, v => Config.Data.IdleBob = v),
                            ModSettingsText.Literal("球慢慢上下起伏。幅度见下方「浮动幅度」。"),
                            null);

                        section.AddToggle("preview_fx",
                            ModSettingsText.Literal("悬停预览特效（含绿色预测球）"),
                            Toggle("preview_fx", () => Config.Data.PreviewEffects, v => Config.Data.PreviewEffects = v),
                            ModSettingsText.Literal("悬停/拖牌时：将要花掉的球发光、波纹拖影 + 星芒，并显示预判出来的新球。"),
                            null);

                        section.AddToggle("gain_fx",
                            ModSettingsText.Literal("获得特效（波纹 / 星芒 / 音符 / 边缘闪）"),
                            Toggle("gain_fx", () => Config.Data.GainEffects, v => Config.Data.GainEffects = v),
                            ModSettingsText.Literal("新点亮或新获得时的一次性波纹与粒子。绿色呼吸是下面单独一项。"),
                            null);

                        section.AddToggle("gain_breath",
                            ModSettingsText.Literal("获得时的绿色呼吸"),
                            Toggle("gain_breath", () => Config.Data.GainBreath, v => Config.Data.GainBreath = v),
                            ModSettingsText.Literal("只影响绿色发光，与上面的获得特效互相独立。"),
                            null);

                        section.AddToggle("precog",
                            ModSettingsText.Literal("音乐牌预知"),
                            Toggle("precog", () => Config.Data.Precognition, v => Config.Data.Precognition = v),
                            ModSettingsText.Literal("悬停一张会把余音顶到 7 的牌时，在角色头顶显示即将从曲库抽出的那一张。"),
                            null);

                        section.AddToggle("consume_burst",
                            ModSettingsText.Literal("消耗碎屑"),
                            Toggle("consume_burst", () => Config.Data.ConsumeBurst, v => Config.Data.ConsumeBurst = v),
                            ModSettingsText.Literal("球灭掉时炸开的小碎屑。"),
                            null);

                        section.AddToggle("hide_after_play",
                            ModSettingsText.Literal("出牌后淡出 / 选中淡入"),
                            Toggle("hide_after_play", () => Config.Data.HideAfterPlay, v => Config.Data.HideAfterPlay = v),
                            ModSettingsText.Literal("出牌后整排淡出 0.18 s，再次悬停手牌时淡回来。"),
                            null);

                        section.AddToggle("avoid_head",
                            ModSettingsText.Literal("避让探头小人（实验性）"),
                            Toggle("avoid_head", () => Config.Data.AvoidHead, v => Config.Data.AvoidHead = v),
                            ModSettingsText.Literal("余音角色卡面小人露头时把球排抬到头顶上方（默认开；觉得不准可以关掉）。"),
                            null);
                    })
                    .AddSection("hand_pips_size", section =>
                    {
                        section.WithTitle(ModSettingsText.Literal("尺寸 / 手感"));

                        section.AddIntSlider("orb_pixels",
                            ModSettingsText.Literal("主能量球径"),
                            Int("orb_pixels", () => Config.Data.OrbPixels, v => Config.Data.OrbPixels = v),
                            32, 64, 1, v => v + " px",
                            ModSettingsText.Literal("46 = 原版基准。手牌多时会自动再缩一点。"));

                        section.AddIntSlider("secondary_pixels",
                            ModSettingsText.Literal("副能量球径（余音 / 星星）"),
                            Int("secondary_pixels", () => Config.Data.SecondaryPixels, v => Config.Data.SecondaryPixels = v),
                            32, 64, 1, v => v + " px",
                            ModSettingsText.Literal("和主能量分开调，默认与主能量同尺寸。"));

                        section.AddIntSlider("secondary_slots",
                            ModSettingsText.Literal("副能量格数（暗球）"),
                            Int("secondary_slots", () => Config.Data.SecondarySlots, v => Config.Data.SecondarySlots = v),
                            0, 12, 1, v => v == 0 ? "只画已有的" : v + " 格",
                            ModSettingsText.Literal("固定显示几格，未点亮的画成暗球；默认 6 格，和主能量的暗格一致。"));

                        section.AddIntSlider("row_offset",
                            ModSettingsText.Literal("离手牌顶边的高度"),
                            Int("row_offset", () => Config.Data.RowOffsetY, v => Config.Data.RowOffsetY = v),
                            24, 80, 1, v => v + " px",
                            ModSettingsText.Literal("球心到手牌最高那张顶边的距离。"));

                        section.AddIntSlider("row_spacing",
                            ModSettingsText.Literal("行内间距"),
                            Int("row_spacing", () => Config.Data.RowSpacing, v => Config.Data.RowSpacing = v),
                            34, 60, 1, v => v + " px",
                            ModSettingsText.Literal("50 = 原版基准。比球径小太多会黏在一起。"));

                        section.AddIntSlider("idle_pixels",
                            ModSettingsText.Literal("浮动幅度"),
                            Int("idle_pixels", () => Config.Data.IdleBobPixels, v => Config.Data.IdleBobPixels = v),
                            0, 6, 1, v => v + " px",
                            ModSettingsText.Literal("待机浮动的上下幅度，0 = 完全静止。"));

                        section.AddIntSlider("burst_count",
                            ModSettingsText.Literal("碎屑数量"),
                            Int("burst_count", () => Config.Data.BurstCount, v => Config.Data.BurstCount = v),
                            4, 32, 1, v => v.ToString(),
                            ModSettingsText.Literal("每颗球消耗时炸开的碎片数。"));

                        section.AddSlider("ripple_scale",
                            ModSettingsText.Literal("波纹大小"),
                            new FloatBinding("ripple_scale", () => Config.Data.RippleScale, v => Config.Data.RippleScale = v),
                            1.2, 4.0, 0.1, v => v.ToString("0.0") + "×",
                            ModSettingsText.Literal("2.8 = 原版。越大扩散得越远。"));

                        section.AddSlider("energy_opacity",
                            ModSettingsText.Literal("主能量不透明度"),
                            new FloatBinding("energy_opacity", () => Config.Data.EnergyOpacity, v => Config.Data.EnergyOpacity = v),
                            0.3, 1.0, 0.05, v => (v * 100).ToString("0") + "%",
                            ModSettingsText.Literal("主能量行单独调，觉得挡视野就调低。"));

                        section.AddSlider("secondary_opacity",
                            ModSettingsText.Literal("副能量不透明度"),
                            new FloatBinding("secondary_opacity", () => Config.Data.SecondaryOpacity, v => Config.Data.SecondaryOpacity = v),
                            0.3, 1.0, 0.05, v => (v * 100).ToString("0") + "%",
                            ModSettingsText.Literal("副资源行（余音 / 星星）单独调。"));
                    })
                    .AddSection("hand_pips_layout", section =>
                    {
                        section.WithTitle(ModSettingsText.Literal("布局"));

                        section.AddEnumChoice("align",
                            ModSettingsText.Literal("两排怎么对齐"),
                            new Binding<RowAlign>("align", () => Config.Data.Align, v => Config.Data.Align = v),
                            v => ModSettingsText.Literal(v switch
                            {
                                RowAlign.Left => "左缘对齐",
                                RowAlign.Right => "右缘对齐",
                                _ => "居中对齐",
                            }),
                            ModSettingsText.Literal("指主能量行和副能量行之间的相对对齐；自由摆放模式下同样生效。"),
                            ModSettingsChoicePresentation.Stepper);

                        section.AddIntSlider("max_per_row",
                            ModSettingsText.Literal("每行最多几颗（0–99，0 = 不限）"),
                            Int("max_per_row", () => Config.Data.MaxPerRow, v => Config.Data.MaxPerRow = v),
                            0, 99, 1, v => v == 0 ? "不限" : v + " 颗",
                            ModSettingsText.Literal("超出就往上换行；主能量和副能量各自换行，不会混到同一行。"));

                        section.AddToggle("free_place",
                            ModSettingsText.Literal("自由摆放"),
                            Toggle("free_place", () => Config.Data.FreePlace, v => Config.Data.FreePlace = v),
                            ModSettingsText.Literal("打开后在战斗里直接按住球排就能拖；位置跨战斗保留。"),
                            null);

                        section.AddButton("reset_place",
                            ModSettingsText.Literal("位置"),
                            ModSettingsText.Literal("回到手牌上方"),
                            () =>
                            {
                                Config.Data.FreePlace = false;
                                Config.Data.FreeX = 0f;
                                Config.Data.FreeY = 0f;
                                Config.Data.FreeScale = 1f;
                                Config.Save();
                                ModEntry.ApplyLive();
                                GD.Print("[BangDreamInSpire/handpips] placement reset to hand");
                            },
                            ModSettingsButtonTone.Normal,
                            ModSettingsText.Literal("关掉自由摆放并清空保存的坐标。"));
                    })
                    .AddSection("hand_pips_maintenance", section =>
                    {
                        section.WithTitle(ModSettingsText.Literal("维护"));

                        section.AddButton("reset_defaults",
                            ModSettingsText.Literal("全部设置"),
                            ModSettingsText.Literal("恢复默认"),
                            () =>
                            {
                                // 只重置观感/数值，保留自由摆放的坐标与缩放，免得摆好的位置白丢
                                float fx = Config.Data.FreeX;
                                float fy = Config.Data.FreeY;
                                float fs = Config.Data.FreeScale;
                                bool free = Config.Data.FreePlace;

                                Config.Replace(new Settings
                                {
                                    FreeX = fx,
                                    FreeY = fy,
                                    FreeScale = fs,
                                    FreePlace = free,
                                });
                                Config.Data.Sanitize();
                                Config.Save();
                                ModEntry.ApplyLive();
                                GD.Print("[BangDreamInSpire/handpips] settings reset to defaults (placement kept)");
                            },
                            ModSettingsButtonTone.Normal,
                            ModSettingsText.Literal("恢复全部默认值，但保留你摆好的位置/缩放。"));
                    });
            }

            GD.Print($"[{ModEntry.ModId}] hand-pips settings appended");
        }
        catch (Exception e)
        {
            GD.PushWarning($"[{ModEntry.ModId}] settings page: {e.Message}");
        }
    }
}
