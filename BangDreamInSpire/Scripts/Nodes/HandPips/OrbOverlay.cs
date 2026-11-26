using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    /// <summary>AtlasTexture 取图要回到图集再裁区域，否则 GetImage() 返回 null。</summary>
    private static Image SafeImage(Texture2D texture)
    {
        // 图集纹理不能直接 GetImage()（会报 "Cannot blit_rect in compressed image formats"），
        // 必须回图集取图再裁区域。
        if (texture is AtlasTexture)
        {
            return AtlasRegionImage((AtlasTexture)texture);
        }

        try
        {
            Image direct = texture.GetImage();
            if (direct != null)
            {
                return direct;
            }
        }
        catch
        {
            // 走图集兜底
        }

        return null;
    }

    private static Image AtlasRegionImage(AtlasTexture atlas)
    {
        {
            try
            {
                if (Config.Data.Debug)
                {
                    GD.Print($"[BangDreamInSpire/handpips] atlas region={atlas.Region} atlasSize=" +
                             $"{(atlas.Atlas != null ? atlas.Atlas.GetWidth() + "x" + atlas.Atlas.GetHeight() : "?")}");
                }

                Image baseImage = atlas.Atlas?.GetImage();
                if (baseImage != null)
                {
                    if (baseImage.IsCompressed())
                    {
                        baseImage.Decompress();
                    }

                    Rect2 region = atlas.Region;
                    if (region.Size.X >= 1f && region.Size.Y >= 1f)
                    {
                        return baseImage.GetRegion(new Rect2I(
                            (int)region.Position.X, (int)region.Position.Y,
                            (int)region.Size.X, (int)region.Size.Y));
                    }

                    return baseImage;
                }
            }
            catch
            {
                // 放弃
            }
        }

        return null;
    }

    /// <summary>图标主色调：按饱和度加权平均非透明像素，归一化后用作光晕颜色。</summary>
    private static Color DominantColor(Texture2D texture, Color fallback)
    {
        return TryDominantColor(texture, out Color color) ? color : fallback;
    }

    private static bool TryDominantColor(Texture2D texture, out Color result)
    {
        result = FallbackGlow;
        ulong key = texture.GetInstanceId();
        if (DominantCache.TryGetValue(key, out Color cached))
        {
            result = cached;
            return true;
        }

        try
        {
            Image image = SafeImage(texture);
            if (image != null)
            {
                if (image.IsCompressed())
                {
                    image.Decompress();
                }

                int w = image.GetWidth();
                int h = image.GetHeight();
                double r = 0, g = 0, b = 0, total = 0;

                for (int y = 0; y < h; y += 3)
                {
                    for (int x = 0; x < w; x += 3)
                    {
                        Color c = image.GetPixel(x, y);
                        if (c.A < 0.35f)
                        {
                            continue;
                        }

                        float max = Mathf.Max(c.R, Mathf.Max(c.G, c.B));
                        float min = Mathf.Min(c.R, Mathf.Min(c.G, c.B));
                        double weight = c.A * (0.25 + (max - min)) * (0.35 + max);
                        r += c.R * weight;
                        g += c.G * weight;
                        b += c.B * weight;
                        total += weight;
                    }
                }

                if (Config.Data.Debug)
                {
                    GD.Print($"[BangDreamInSpire/handpips] dominant '{texture.ResourcePath}' {w}x{h} total={total:F2} fmt={image.GetFormat()}");
                }

                if (total > 0.0001)
                {
                    var avg = new Color((float)(r / total), (float)(g / total), (float)(b / total));
                    float max = Mathf.Max(avg.R, Mathf.Max(avg.G, avg.B));
                    if (max > 0.01f)
                    {
                        avg = new Color(avg.R / max, avg.G / max, avg.B / max);
                    }

                    result = avg.Lerp(Colors.White, 0.15f);
                    DominantCache[key] = result;
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// 只对 Bang 系角色（本 mod 的角色）挂资源球；其它角色完全不介入。
    /// 判定优先看「实现了本 mod 的角色能力接口」——这样以后新增角色不用改这里；
    /// 程序集名只作为旧角色的兜底。
    /// </summary>
    private static bool IsBangCharacter(Player player)
    {
        try
        {
            var character = player?.Character;
            if (character == null)
            {
                return false;
            }

            if (character is BangDreamInSpire.Scripts.Interfaces.CharacterAugment.ILingeredResourceCharacter
                or BangDreamInSpire.Scripts.Interfaces.CharacterAugment.IPerformableCharacter
                or BangDreamInSpire.Scripts.Interfaces.CharacterAugment.IPuppeteerCharacter
                or BangDreamInSpire.Scripts.Interfaces.CharacterAugment.IGroupableCharacter
                or BangDreamInSpire.Scripts.Interfaces.CharacterAugment.IExtraDeckSupportCharacter
                or BangDreamInSpire.Scripts.Interfaces.CharacterAugment.ISkinSupportCharacter)
            {
                return true;
            }

            var assembly = character.GetType().Assembly.GetName().Name ?? string.Empty;
            return assembly is "BangDreamInSpire" or "ItsCrychic" or "MutsumiNoDeck" or "TogawaSakiko";
        }
        catch
        {
            return false;
        }
    }

    private static Overlay OverlayFor(Player player, NPlayerHand hand)
    {
        if (!IsBangCharacter(player))
        {
            return null;
        }

        if (Overlays.TryGetValue(player, out Overlay overlay)
            && GodotObject.IsInstanceValid(overlay.Root)
            && overlay.Root.IsInsideTree())
        {
            return overlay;
        }

        Node2D root = null;

        try
        {
            // 和原版一模一样：挂成手牌的最后一个子节点，不设 ZIndex。
            // 手牌 z=0、悬停那张 z=1 —— 所以悬停放大时会盖住球，平时球在牌上面。
            root = new Node2D { Name = "HandPipsOrbs" };
            hand.AddChild(root);

            var energyRow = new Node2D { Name = "Energy" };
            root.AddChild(energyRow);

            var created = new Overlay { Root = root, Energy = { Root = energyRow } };

            // 等比缩放框：普通引擎节点（Line2D / Polygon2D），不依赖自定义类
            var gripRoot = new Node2D { Name = "HandPipsGrip", Visible = false };
            root.AddChild(gripRoot);
            created.GripRoot = gripRoot;
            created.GripTop = MakeGripEdge(gripRoot);
            created.GripBottom = MakeGripEdge(gripRoot);
            created.GripLeft = MakeGripEdge(gripRoot);
            created.GripRight = MakeGripEdge(gripRoot);

            for (int i = 0; i < 4; i++)
            {
                var handle = new Polygon2D
                {
                    Color = new Color(1f, 1f, 1f, 0.32f),
                    Polygon = new[]
                    {
                        new Vector2(-4f, -4f),
                        new Vector2(4f, -4f),
                        new Vector2(4f, 4f),
                        new Vector2(-4f, 4f),
                    },
                };
                gripRoot.AddChild(handle);
                created.GripHandles[i] = handle;
            }

            try
            {
                var watchdog = new CombatWatchdog { Player = player, Hand = hand, Root = root };
                watchdog.Attach(root.GetTree());
            }
            catch
            {
                // 拿不到 SceneTree 就算了，Tick 里的 Hide 仍会兜底
            }

            Overlays[player] = created;
            return created;
        }
        catch (Exception e)
        {
            GD.PushWarning($"[BangDreamInSpire/handpips] orb overlay: {e.Message}");

            // 已经挂进树的半成品必须自己回收，否则下一帧会在同一父节点下再挂一棵
            if (root != null && GodotObject.IsInstanceValid(root))
            {
                root.QueueFree();
            }

            return null;
        }
    }

    private static Texture2D ResolveEnergyTexture(Player player, Texture2D captured, NEnergyCounter counter)
    {
        // 顺序很重要：角色卡池里的能量图标路径最可靠，其次从计数器子树里按名字打分挑，
        // 最后才用 _Ready 抓到的"第一个 Sprite"——它经常是白径向的 glow 贴图，
        // 一旦优先用它，球就只剩一团白光、没有图标。
        foreach (string path in EnergyIconPaths(player))
        {
            Texture2D texture = LoadTexture(path);
            if (Config.Data.Debug)
            {
                GD.Print($"[BangDreamInSpire/handpips] icon candidate '{path}' -> {(texture != null ? "ok" : "null")}");
            }

            if (texture != null)
            {
                return texture;
            }
        }

        Texture2D fromCounter = ChooseEnergyIcon(counter);
        if (Config.Data.Debug)
        {
            GD.Print($"[BangDreamInSpire/handpips] icon from counter -> {(fromCounter != null ? fromCounter.ResourcePath : "null")}");
        }

        if (fromCounter != null)
        {
            return fromCounter;
        }

        if (Config.Data.Debug)
        {
            GD.Print($"[BangDreamInSpire/handpips] icon fallback captured -> {(captured != null ? captured.ResourcePath : "null")}");
        }

        return captured;
    }

    private static Texture2D ChooseEnergyIcon(Node counter)
    {
        Texture2D best = null;
        int bestScore = int.MinValue;

        foreach ((Texture2D texture, string name, int depth) in CounterTextures(counter))
        {
            if (texture == null)
            {
                continue;
            }

            string path = texture.ResourcePath ?? string.Empty;
            int score = 0;

            if (path.Contains("energy", StringComparison.OrdinalIgnoreCase)
                || name.Contains("energy", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            int width = texture.GetWidth();
            if (width >= 32 && width <= 160)
            {
                score += 20;
            }

            if (width > 512)
            {
                score -= 40;
            }

            if (path.Contains("glow", StringComparison.OrdinalIgnoreCase)
                || name.Contains("glow", StringComparison.OrdinalIgnoreCase)
                || path.Contains("shadow", StringComparison.OrdinalIgnoreCase)
                || name.Contains("shadow", StringComparison.OrdinalIgnoreCase))
            {
                score -= 60;
            }

            score -= depth;

            if (score > bestScore)
            {
                bestScore = score;
                best = texture;
            }
        }

        return best;
    }

    /// <summary>计数器上的图标可能是 Sprite2D，也可能是 TextureRect，两种都收。</summary>
    private static IEnumerable<(Texture2D, string, int)> CounterTextures(Node node, int depth = 0)
    {
        if (node == null || depth > 6)
        {
            yield break;
        }

        foreach (Node child in node.GetChildren(false))
        {
            switch (child)
            {
                case Sprite2D sprite when sprite.Texture != null:
                    yield return (sprite.Texture, sprite.Name.ToString(), depth);
                    break;
                case TextureRect rect when rect.Texture != null:
                    yield return (rect.Texture, rect.Name.ToString(), depth);
                    break;
                case TextureButton button when button.TextureNormal != null:
                    yield return (button.TextureNormal, button.Name.ToString(), depth);
                    break;
            }

            foreach ((Texture2D, string, int) nested in CounterTextures(child, depth + 1))
            {
                yield return nested;
            }
        }
    }

    internal static Texture2D FirstSprite(Node node, int depth = 0)
    {
        if (node == null || depth > 6)
        {
            return null;
        }

        if (node is Sprite2D sprite && sprite.Texture != null)
        {
            return sprite.Texture;
        }

        foreach (Node child in node.GetChildren(false))
        {
            Texture2D found = FirstSprite(child, depth + 1);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnergyIconPaths(Player player)
    {
        var paths = new List<string>();

        try
        {
            string byPool = EnergyIconHelper.GetPath(player.Character.CardPool);
            if (!string.IsNullOrEmpty(byPool))
            {
                paths.Add(byPool);
            }
        }
        catch
        {
            // 卡池未注册时跳过
        }

        paths.Add("res://images/packed/sprite_fonts/regent_energy_icon.png");
        return paths;
    }

    public static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return PreloadManager.Cache?.GetTexture2D(path) ?? ResourceLoader.Load<Texture2D>(path);
        }
        catch
        {
            return null;
        }
    }

    private static void Hide(Player player)
    {
        if (player != null && Overlays.TryGetValue(player, out Overlay overlay)
            && GodotObject.IsInstanceValid(overlay.Root))
        {
            overlay.Root.Visible = false;
            overlay.WasHidden = true;
        }
    }
}
