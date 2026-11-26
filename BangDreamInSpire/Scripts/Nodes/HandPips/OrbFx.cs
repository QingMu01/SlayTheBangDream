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
    private static readonly System.Random SparkRng = new System.Random(20261122);

    private static void ClearSparks(Row row)
    {
        foreach (Spark spark in row.Sparks)
        {
            if (spark.Root != null && GodotObject.IsInstanceValid(spark.Root))
            {
                spark.Root.QueueFree();
            }
        }

        row.Sparks.Clear();
    }

    /// <summary>主能量行：星座（星点 + 连线）；副资源行：一圈音符。</summary>
    private static void SpawnSparks(Row row, int fromIndex = 0, int toIndexExclusive = -1)
    {
        if (row.Root == null || !GodotObject.IsInstanceValid(row.Root))
        {
            return;
        }

        ClearSparks(row);
        // 实际球径：由 Row.LastOrbPx 提供（副资源行已经含 0.85 行缩放，不再自己乘）
        float orbPx = row.LastOrbPx;

        int first = Mathf.Clamp(fromIndex, 0, Mathf.Max(0, row.Pips.Count - 1));
        int last = toIndexExclusive >= 0 ? Math.Min(toIndexExclusive, row.Pips.Count) : row.Pips.Count;

        for (int p = first; p < last; p++)
        {
            Pip pip = row.Pips[p];
            if (pip.Root == null || !GodotObject.IsInstanceValid(pip.Root) || !pip.Root.Visible)
            {
                continue;
            }

            Vector2 anchor = pip.Root.Position;

            if (row.IsSecondary)
            {
                // 余音：回到"一圈均分、依次弹出"的旧运动感，只把符号做成 5 选 1
                for (int i = 0; i < 5; i++)
                {
                    float ang = i * Mathf.Tau / 5f + (float)(SparkRng.NextDouble() - 0.5) * 0.2f;
                    float rad = orbPx * (0.85f + (float)SparkRng.NextDouble() * 0.2f);
                    float size = 0.75f + (float)SparkRng.NextDouble() * 0.35f;
                    AddSpark(row, NewSpark(row, NoteAt(SparkRng.Next(5)), anchor, ang, rad, size, i * 0.03f));
                }

                continue;
            }

            // 主能量：球心大十字星 + 一圈 8 星 + 其中几条连线（星座感）
            AddSpark(row, NewSpark(row, Star4Texture, anchor, 0f, 0f, 2.6f, 0f));

            var ring = new List<Spark>();
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.Tau / 8f + (float)(SparkRng.NextDouble() - 0.5) * 0.28f;
                float rad = orbPx * (0.6f + (float)SparkRng.NextDouble() * 0.3f);
                float size = 0.55f + (float)SparkRng.NextDouble() * 0.5f;
                Spark s = NewSpark(row, Star4Texture, anchor, ang, rad, size,
                    (float)SparkRng.NextDouble() * 0.08f);
                if (s == null)
                {
                    continue;
                }

                // 主能量的星芒保留随机朝向与轻微自旋（YG 认可这版）
                s.Spin = ((float)SparkRng.NextDouble() * 2f - 1f) * 0.7f;
                s.Rotate = (float)SparkRng.NextDouble() * Mathf.Tau;

                row.Sparks.Add(s);
                ring.Add(s);
            }

            for (int k = 0; k < 3; k++)
            {
                Spark a = ring[SparkRng.Next(ring.Count)];
                Spark b = ring[SparkRng.Next(ring.Count)];
                if (a == b)
                {
                    continue;
                }

                var line = new Line2D
                {
                    Width = 1f,
                    DefaultColor = Colors.White,
                    Antialiased = true,
                    Points = new[] { Vector2.Zero, Vector2.Zero },
                };

                row.Root.AddChild(line);
                row.Sparks.Add(new Spark
                {
                    Root = line,
                    Line = line,
                    A = a,
                    B = b,
                    Anchor = anchor,
                    Delay = Mathf.Max(a.Delay, b.Delay) + 0.03f,
                    Size = 1f,
                    IsLine = true,
                });
            }
        }
    }


    private static void AddSpark(Row row, Spark spark)
    {
        if (spark != null)
        {
            row.Sparks.Add(spark);
        }
    }

    private static Spark NewSpark(Row row, Texture2D texture, Vector2 anchor, float angle, float radius, float size, float delay)
    {
        var root = new Node2D();
        var sprite = new Sprite2D
        {
            Texture = texture,
            Modulate = Colors.Transparent,
        };
        root.AddChild(sprite);

        if (!TryAttach(row.Root, root))
        {
            return null;
        }

        return new Spark
        {
            Root = root,
            Sprite = sprite,
            Anchor = anchor,
            Angle = angle,
            Radius = radius,
            Size = size,
            Delay = delay,
            Life = 0f,
        };
    }

    private static void UpdateSparks(Row row, float delta)
    {
        if (row.Sparks.Count == 0)
        {
            return;
        }

        float orbPx = row.LastOrbPx;

        for (int i = row.Sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = row.Sparks[i];
            if (spark == null || spark.Root == null || !GodotObject.IsInstanceValid(spark.Root))
            {
                row.Sparks.RemoveAt(i);
                continue;
            }

            spark.Life += delta;
            float p = (spark.Life - spark.Delay) / 1.1f;
            if (p < 0f)
            {
                if (spark.Sprite != null)
                {
                    spark.Sprite.Modulate = Colors.Transparent;
                }

                continue;
            }

            float fade = Mathf.Min(1f, p / 0.15f) * (1f - Mathf.Max(0f, (p - 0.55f) / 0.45f));
            if (p > 1f || fade <= 0.01f)
            {
                spark.Root.QueueFree();
                row.Sparks.RemoveAt(i);
                continue;
            }

            float grow = 0.78f + 0.55f * (1f - Mathf.Pow(1f - p, 2f));
            float angle = spark.Angle + p * (spark.Spin == 0f ? 0.6f : spark.Spin);
            var pos = spark.Anchor + new Vector2(
                Mathf.Cos(angle) * spark.Radius * grow,
                Mathf.Sin(angle) * spark.Radius * grow * 0.92f);

            Color accent = row.Glow;
            if (spark.IsLine)
            {
                if (spark.A != null && spark.B != null
                    && GodotObject.IsInstanceValid(spark.A.Root) && GodotObject.IsInstanceValid(spark.B.Root))
                {
                    spark.Line.SetPointPosition(0, spark.A.Root.Position);
                    spark.Line.SetPointPosition(1, spark.B.Root.Position);
                    spark.Line.Modulate = new Color(accent, fade * 0.35f);
                }

                continue;
            }

            spark.Root.Position = pos;
            if (spark.Sprite != null && (spark.Rotate != 0f || spark.Spin != 0f))
            {
                spark.Sprite.Rotation = spark.Rotate + p * spark.Spin * 1.6f;
            }
            float tw = 0.55f + 0.45f * Mathf.Sin(p * 6.2f + spark.Angle * 3f);
            spark.Sprite.Modulate = new Color(accent.Lerp(Colors.White, 0.35f), fade * tw);
            float px = (row.IsSecondary ? 16f : 11f) * spark.Size * (0.8f + 0.45f * Mathf.Sin(Mathf.Pi * p));
            spark.Sprite.Scale = Vector2.One * (px / 64f);
        }
    }

    /// <summary>兑现（余音攒满 7 被吃掉）时炸开一圈随机音符，而不是多边形碎屑。</summary>
    private static void SpawnNoteBurst(Row row, Vector2 at, int count)
    {
        if (row.Root == null || !GodotObject.IsInstanceValid(row.Root))
        {
            return;
        }

        int n = Mathf.Clamp(count, 4, 16);
        for (int i = 0; i < n; i++)
        {
            float ang = i * Mathf.Tau / n + (float)(SparkRng.NextDouble() - 0.5) * 0.35f;
            float radius = 24f + (float)SparkRng.NextDouble() * 26f;
            float size = 1.05f + (float)SparkRng.NextDouble() * 0.7f;
            Texture2D note = NoteAt(SparkRng.Next(3));
            Spark spark = NewSpark(row, note, at, ang, radius, size, i * 0.025f);
            if (spark != null)
            {
                row.Sparks.Add(spark);
            }
        }
    }
    private static void SpawnBurst(Row row, Vector2 at, int count)
    {
        if (row.Root == null || !GodotObject.IsInstanceValid(row.Root))
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float ang = (float)(SparkRng.NextDouble() * Mathf.Tau);
            float spd = 30f + (float)SparkRng.NextDouble() * 62f;
            float sr = 0.9f + (float)SparkRng.NextDouble() * 1.5f;
            int sides = 3 + SparkRng.Next(2);
            var pts = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k * Mathf.Tau / sides + (float)(SparkRng.NextDouble() - 0.5) * 0.6f;
                float rr = sr * (0.7f + (float)SparkRng.NextDouble() * 0.5f);
                pts[k] = new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
            }

            var shard = new Polygon2D
            {
                Polygon = pts,
                Color = new Color(row.Glow.Lerp(Colors.White, 0.3f), 0.95f),
                Position = at,
                Rotation = (float)(SparkRng.NextDouble() * Mathf.Tau),
            };

            shard.SetMeta(M_Vx, Mathf.Cos(ang) * spd);
            shard.SetMeta(M_Vy, Mathf.Sin(ang) * spd);
            shard.SetMeta(M_Life, 0.9f);
            shard.SetMeta(M_Spin, (float)(SparkRng.NextDouble() - 0.5) * 9f);
            row.Root.AddChild(shard);
            row.BurstFx.Add(shard);
        }
    }


    private static void UpdateBurst(Row row, float delta)
    {
        for (int i = row.BurstFx.Count - 1; i >= 0; i--)
        {
            Node2D node = row.BurstFx[i];
            if (node == null || !GodotObject.IsInstanceValid(node))
            {
                row.BurstFx.RemoveAt(i);
                continue;
            }

            float life = node.GetMeta(M_Life).AsSingle() - delta;
            if (life <= 0f)
            {
                node.QueueFree();
                row.BurstFx.RemoveAt(i);
                continue;
            }

            node.SetMeta(M_Life, life);
            float vx = node.GetMeta(M_Vx).AsSingle();
            float vy = node.GetMeta(M_Vy).AsSingle();
            node.Position += new Vector2(vx, vy) * delta;
            node.Rotation += node.GetMeta(M_Spin).AsSingle() * delta;

            float k = life / 0.9f;
            if (node is Polygon2D poly)
            {
                poly.Color = new Color(poly.Color.R, poly.Color.G, poly.Color.B, 0.95f * k * k);
                poly.Scale = Vector2.One * (0.6f + 0.4f * k);
            }
            else
            {
                node.Modulate = new Color(1f, 1f, 1f, k);
            }
        }
    }

    private static void DrawEcho(Sprite2D sprite, float p, float alphaScale, float orbPx, float rootScale,
        Color color, float maxScale)
    {
        if (sprite == null)
        {
            return;
        }

        p = Mathf.PosMod(p, 1f);
        // 向内收：越接近结束越亮，最后 15% 快速消失
        float a = 0.62f * alphaScale * (0.35f + 0.65f * p)
                  * (1f - Mathf.Max(0f, (p - 0.85f) / 0.15f));
        if (a <= 0.015f)
        {
            sprite.Visible = false;
            return;
        }

        float dia = orbPx * (1f + Mathf.Max(0f, maxScale - 1f) * (1f - p));
        sprite.Visible = true;
        sprite.Modulate = new Color(color, a);
        sprite.Scale = Vector2.One * (dia / (104f * rootScale));
    }

    private static Pip NewPip(Row row)
    {
        var root = new Node2D();

        var glow = new Sprite2D
        {
            Texture = GlowTexture,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            Scale = Vector2.One * 0.6828125f,
        };

        var icon = new Sprite2D
        {
            Texture = row.Texture,
            Scale = Vector2.One * (46f / Math.Max(1, Math.Max(row.Texture.GetWidth(), row.Texture.GetHeight()))),
            Material = new ShaderMaterial { Shader = ToneShader },
        };

        var shine = new Sprite2D
        {
            Texture = row.Texture,
            Scale = icon.Scale,
            Visible = false,
            Material = new ShaderMaterial { Shader = ShineShader },
        };

        var plus = new Sprite2D
        {
            Texture = PlusTexture,
            Scale = Vector2.One * 0.34f,
            Position = new Vector2(11f, -11f),
            Visible = false,
            Modulate = PlusGreen,
        };

        var ring = new Sprite2D
        {
            Texture = RingTexture,
            Scale = Vector2.One * 0.5f,
            Visible = false,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };

        root.AddChild(glow);
        root.AddChild(icon);
        root.AddChild(shine);
        root.AddChild(plus);
        root.AddChild(ring);

        var gem = new Sprite2D
        {
            Texture = Star4Texture,
            Visible = false,
            Position = new Vector2(10f, -10f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        root.AddChild(gem);

        var edge = new Sprite2D
        {
            Texture = RingTexture,
            Visible = false,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        root.AddChild(edge);

        var dashRing = new Sprite2D
        {
            Texture = RingTexture,
            Visible = false,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        root.AddChild(dashRing);

        // 三层内收拖影的中间层：独立节点，避免和 Edge（刷新闪光）抢同一个 Sprite
        var echoMid = new Sprite2D
        {
            Texture = RingTexture,
            Visible = false,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        root.AddChild(echoMid);

        var sheen = new Sprite2D
        {
            Texture = row.Texture,
            Scale = icon.Scale,
            Visible = false,
            Material = new ShaderMaterial { Shader = SheenShader },
        };
        root.AddChild(sheen);

        if (!TryAttach(row.Root, root))
        {
            return null;
        }

        return new Pip
        {
            Root = root,
            Icon = icon,
            Glow = glow,
            Shine = shine,
            Plus = plus,
            Ring = ring,
            Gem = gem,
            Edge = edge,
            DashRing = dashRing,
            EchoMid = echoMid,
            Sheen = sheen,
            Tone = (ShaderMaterial)icon.Material,
            Seed = row.Pips.Count,
        };
    }
}
