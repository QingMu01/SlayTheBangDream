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
    private const string ToneCode =
        "shader_type canvas_item;\n" +
        "uniform float grey = 0.0;\n" +
        "uniform vec3 tint = vec3(1.0);\n" +
        "uniform float white = 0.0;\n" +
        "void fragment() {\n" +
        "    float l = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114));\n" +
        "    COLOR.rgb = mix(mix(COLOR.rgb, vec3(l), grey) * tint, vec3(1.0), white);\n" +
        "}";

    private const string ShineCode =
        "shader_type canvas_item;\n" +
        "render_mode blend_add;\n" +
        "uniform float strength = 0.0;\n" +
        "uniform vec3 tint = vec3(1.0, 0.97, 0.9);\n" +
        "void fragment() {\n" +
        "    vec4 t = texture(TEXTURE, UV);\n" +
        "    float l = dot(t.rgb, vec3(0.299, 0.587, 0.114));\n" +
        "    COLOR = vec4((0.35 + l) * strength * tint, t.a);\n" +
        "}";

    private static Shader _tone;
    private static Shader _shine;
    private static Texture2D _glow;
    private static Texture2D _plus;

    private static Shader ToneShader => _tone ??= new Shader { Code = ToneCode };

    private static Shader ShineShader => _shine ??= new Shader { Code = ShineCode };

    private static Texture2D GlowTexture
    {
        get
        {
            if (_glow != null)
            {
                return _glow;
            }

            var gradient = new Gradient
            {
                Colors = new[]
                {
                    new Color(1f, 1f, 1f, 0.9f),
                    new Color(1f, 1f, 1f, 0.35f),
                    new Color(1f, 1f, 1f, 0f),
                },
                Offsets = new[] { 0f, 0.35f, 1f },
            };

            _glow = new GradientTexture2D
            {
                Width = 128,
                Height = 128,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
                Gradient = gradient,
            };
            return _glow;
        }
    }

    private const string SheenCode =
        "shader_type canvas_item;\n" +
        "render_mode blend_add;\n" +
        "uniform float phase = -1.0;\n" +
        "uniform float strength = 0.0;\n" +
        "uniform vec3 tint = vec3(1.0);\n" +
        "void fragment() {\n" +
        "    vec4 t = texture(TEXTURE, UV);\n" +
        "    float d = (UV.x + UV.y * 0.7) - phase;\n" +
        "    float band = smoothstep(0.085, 0.015, abs(d));\n" +
        "    COLOR = vec4(tint * band * strength, t.a);\n" +
        "}";

    private static Shader _sheen;

    private static Shader SheenShader => _sheen ??= new Shader { Code = SheenCode };

    private static Texture2D _star4;
    private static Texture2D _note;

    private static Texture2D Star4Texture
    {
        get
        {
            if (_star4 != null)
            {
                return _star4;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));
            float cx = size / 2f - 0.5f, cy = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 星芒：四臂从中心向外收细，末端渐隐（不是两条等宽细杆）
                    float dx = x - cx;
                    float dy = y - cy;
                    float u = (dx + dy) * 0.7071f;
                    float v = (dx - dy) * 0.7071f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp(r / 30f, 0f, 1f);
                    float width = Mathf.Lerp(3.4f, 0.35f, t);    // 靠近中心粗、越往外越细
                    float tip = Mathf.Lerp(1f, 0.45f, t);         // 末端只轻微渐隐（保住体量）
                    float arm1 = Mathf.Clamp(1f - Mathf.Abs(v) / Mathf.Max(0.05f, width), 0f, 1f) * tip;
                    float arm2 = Mathf.Clamp(1f - Mathf.Abs(u) / Mathf.Max(0.05f, width), 0f, 1f) * tip;
                    float core = Mathf.Clamp(1f - r / 3.4f, 0f, 1f);
                    float a = Mathf.Clamp(Mathf.Max(Mathf.Max(arm1, arm2), core), 0f, 1f);
                    a = Mathf.Pow(a, 1.1f);

                    if (a > 0.01f)
                    {
                        image.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
            }

            _star4 = ImageTexture.CreateFromImage(image);
            return _star4;
        }
    }

    private static Texture2D NoteTexture
    {
        get
        {
            if (_note != null)
            {
                return _note;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));

            // 符头（斜椭圆）
            for (int y = 34; y < 52; y++)
            {
                for (int x = 10; x < 34; x++)
                {
                    float nx = (x - 22f) / 11f;
                    float ny = (y - 43f) / 8f;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        image.SetPixel(x, y, Colors.White);
                    }
                }
            }

            // 符干
            for (int y = 10; y < 44; y++)
            {
                for (int x = 29; x < 34; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            // 符尾
            for (int y = 10; y < 26; y++)
            {
                int w = (int)((26 - y) * 0.55f);
                for (int x = 34; x < 34 + w; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            _note = ImageTexture.CreateFromImage(image);
            return _note;
        }
    }

    private static Texture2D _noteQuarter;
    private static Texture2D _noteBeamed;

    private static Texture2D NoteQuarterTexture
    {
        get
        {
            if (_noteQuarter != null)
            {
                return _noteQuarter;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));
            for (int y = 36; y < 52; y++)
            {
                for (int x = 14; x < 36; x++)
                {
                    float nx = (x - 25f) / 10f;
                    float ny = (y - 44f) / 7f;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        image.SetPixel(x, y, Colors.White);
                    }
                }
            }

            for (int y = 12; y < 44; y++)
            {
                for (int x = 31; x < 36; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            _noteQuarter = ImageTexture.CreateFromImage(image);
            return _noteQuarter;
        }
    }

    private static Texture2D NoteBeamedTexture
    {
        get
        {
            if (_noteBeamed != null)
            {
                return _noteBeamed;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));
            // 两个符头
            for (int k = 0; k < 2; k++)
            {
                int cx = 16 + k * 22;
                for (int y = 36; y < 50; y++)
                {
                    for (int x = cx - 10; x < cx + 10; x++)
                    {
                        float nx = (x - cx) / 9f;
                        float ny = (y - 43f) / 6.5f;
                        if (nx * nx + ny * ny <= 1f)
                        {
                            image.SetPixel(x, y, Colors.White);
                        }
                    }
                }

                int stemX = cx + 7;
                for (int y = 14; y < 44; y++)
                {
                    for (int x = stemX; x < stemX + 4; x++)
                    {
                        image.SetPixel(x, y, Colors.White);
                    }
                }
            }

            // 横梁
            for (int y = 14; y < 24; y++)
            {
                for (int x = 23; x < 48; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            _noteBeamed = ImageTexture.CreateFromImage(image);
            return _noteBeamed;
        }
    }

    private static Texture2D _noteDouble;

    /// <summary>十六分音符（双横梁）。</summary>
    private static Texture2D NoteDoubleTexture
    {
        get
        {
            if (_noteDouble != null)
            {
                return _noteDouble;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));

            for (int k = 0; k < 2; k++)
            {
                int cx = 16 + k * 22;
                for (int y = 34; y < 48; y++)
                {
                    for (int x = cx - 10; x < cx + 10; x++)
                    {
                        float nx = (x - cx) / 9f;
                        float ny = (y - 41f) / 6.5f;
                        if (nx * nx + ny * ny <= 1f)
                        {
                            image.SetPixel(x, y, Colors.White);
                        }
                    }
                }

                int stemX = cx + 7;
                for (int y = 10; y < 42; y++)
                {
                    for (int x = stemX; x < stemX + 4; x++)
                    {
                        image.SetPixel(x, y, Colors.White);
                    }
                }
            }

            for (int y = 10; y < 16; y++)
            {
                for (int x = 23; x < 48; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            for (int y = 19; y < 25; y++)
            {
                for (int x = 23; x < 48; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            _noteDouble = ImageTexture.CreateFromImage(image);
            return _noteDouble;
        }
    }

    private static Texture2D _noteDotted;

    /// <summary>附点四分音符（♩.）：干净、和其余三个同风格。</summary>
    private static Texture2D NoteDottedTexture
    {
        get
        {
            if (_noteDotted != null)
            {
                return _noteDotted;
            }

            const int size = 64;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));

            for (int y = 36; y < 52; y++)
            {
                for (int x = 14; x < 36; x++)
                {
                    float nx = (x - 25f) / 10f;
                    float ny = (y - 44f) / 7f;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        image.SetPixel(x, y, Colors.White);
                    }
                }
            }

            for (int y = 12; y < 44; y++)
            {
                for (int x = 31; x < 36; x++)
                {
                    image.SetPixel(x, y, Colors.White);
                }
            }

            // 附点
            for (int y = 38; y < 44; y++)
            {
                for (int x = 42; x < 48; x++)
                {
                    float nx = (x - 45f) / 3f;
                    float ny = (y - 41f) / 3f;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        image.SetPixel(x, y, Colors.White);
                    }
                }
            }

            _noteDotted = ImageTexture.CreateFromImage(image);
            return _noteDotted;
        }
    }

    private static Texture2D NoteAt(int index)
    {
        return (index % 5) switch
        {
            0 => NoteTexture,
            1 => NoteQuarterTexture,
            2 => NoteBeamedTexture,
            3 => NoteDoubleTexture,
            _ => NoteDottedTexture,
        };
    }

    private static Texture2D _ring;

    private static Texture2D RingTexture
    {
        get
        {
            if (_ring != null)
            {
                return _ring;
            }

            const int size = 128;
            const float radius = 52f;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size / 2f + 0.5f;
                    float dy = y - size / 2f + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Clamp(1f - Mathf.Abs(d - radius) / 5f, 0f, 1f);
                    float a = falloff * falloff;
                    if (a > 0.01f)
                    {
                        image.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
            }

            _ring = ImageTexture.CreateFromImage(image);
            return _ring;
        }
    }

    /// <summary>
    /// 原版 OrbPlus 的复刻：深绿描边十字 + 亮绿内芯，24×24 px（相对 46 px 球）。
    /// 原版是 _Draw 画四个矩形，我们这里烘焙成贴图（自定义节点的 _Draw 在 DLL mod 里不派发）。
    /// </summary>
    private static Texture2D PlusTexture
    {
        get
        {
            if (_plus != null)
            {
                return _plus;
            }

            const int size = 32;
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));

            // 淡亮绿，不要深色描边
            var bright = new Color(0.62f, 1f, 0.62f, 0.95f);
            float cx = size / 2f;
            float cy = size / 2f;

            void Fill(float x0, float y0, float w, float h, Color color)
            {
                for (int y = (int)Mathf.Round(y0); y < (int)Mathf.Round(y0 + h); y++)
                {
                    for (int x = (int)Mathf.Round(x0); x < (int)Mathf.Round(x0 + w); x++)
                    {
                        if (x >= 0 && y >= 0 && x < size && y < size)
                        {
                            image.SetPixel(x, y, color);
                        }
                    }
                }
            }

            // 单色十字：横 22×5.2、竖 5.2×22（无描边）
            Fill(cx - 11f, cy - 2.6f, 22f, 5.2f, bright);
            Fill(cx - 2.6f, cy - 11f, 5.2f, 22f, bright);

            _plus = ImageTexture.CreateFromImage(image);
            return _plus;
        }
    }
}