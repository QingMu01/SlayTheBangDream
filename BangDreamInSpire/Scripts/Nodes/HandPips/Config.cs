using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>整排球显示哪几部分。</summary>
public enum OrbDisplayMode
{
    /// <summary>主能量 + 副资源都画。</summary>
    All = 0,

    /// <summary>都不画。</summary>
    None = 1,

    /// <summary>只画主能量。</summary>
    EnergyOnly = 2,

    /// <summary>只画副资源（余音 / 星星）。</summary>
    SecondaryOnly = 3,
}

/// <summary>主能量行与副资源行之间的对齐关系。</summary>
public enum RowAlign
{
    /// <summary>两行中心对齐。</summary>
    Center = 0,

    /// <summary>两行左缘对齐。</summary>
    Left = 1,

    /// <summary>两行右缘对齐。</summary>
    Right = 2,
}

/// <summary>hand_pips.json 的结构；所有字段都能在游戏内「模组配置」里实时改。</summary>
public sealed class Settings
{
    /// <summary>显示哪几部分：全都有 / 全都没有 / 只有主能量 / 只有副能量。</summary>
    public OrbDisplayMode Mode { get; set; } = OrbDisplayMode.All;

    // ─── 特效开关（默认全关，需要再开）───
    /// <summary>待机上下浮动。</summary>
    public bool IdleBob { get; set; } = true;

    /// <summary>悬停/拖动时的三层内收波纹 + 宝石星芒 + 硬光斜扫 + 绿色预测球。</summary>
    public bool PreviewEffects { get; set; } = true;

    /// <summary>音乐牌预知：余音将满 7 时，在角色头顶预告即将从曲库抽出的那张音乐牌。</summary>
    public bool Precognition { get; set; } = true;

    /// <summary>获得时的一次性波纹 + 星芒/音符 + 边缘闪光。</summary>
    public bool GainEffects { get; set; } = true;

    /// <summary>获得时的绿色呼吸（与 GainEffects 独立）。</summary>
    public bool GainBreath { get; set; } = true;

    /// <summary>消耗时炸开的碎屑。</summary>
    public bool ConsumeBurst { get; set; } = true;

    /// <summary>出牌后淡出、下次选中淡入。</summary>
    public bool HideAfterPlay { get; set; } = true;

    /// <summary>探头小人露头时把球排抬到头顶上方并弯成拱形。</summary>
    public bool AvoidHead { get; set; } = true;

    // ─── 尺寸 / 手感 ───
    /// <summary>主能量球的目标像素直径（46 = 原版基准）。</summary>
    public int OrbPixels { get; set; } = 46;

    /// <summary>副资源（余音 / 星星）球的目标像素直径。</summary>
    public int SecondaryPixels { get; set; } = 46;

    /// <summary>副资源行固定显示的格数（未点亮的画成暗球），0 = 只画已有的。</summary>
    public int SecondarySlots { get; set; } = 6;

    /// <summary>球心到手牌顶边的距离。</summary>
    public int RowOffsetY { get; set; } = 44;

    /// <summary>行内球心间距（50 = 原版基准）。</summary>
    public int RowSpacing { get; set; } = 50;

    /// <summary>波纹最大直径（球径的倍数，2.8 = 原版）。</summary>
    public float RippleScale { get; set; } = 2.8f;

    /// <summary>消耗碎屑数量。</summary>
    public int BurstCount { get; set; } = 16;

    /// <summary>待机浮动幅度（像素）。</summary>
    public int IdleBobPixels { get; set; } = 3;

    /// <summary>主能量行不透明度。</summary>
    public float EnergyOpacity { get; set; } = 1f;

    /// <summary>副资源行不透明度。</summary>
    public float SecondaryOpacity { get; set; } = 1f;

    // ─── 布局 ───
    public RowAlign Align { get; set; } = RowAlign.Center;

    /// <summary>每行最多几颗（0–99），超出向上换行；主能量与副资源各自换行、互不混行。0 = 不限。</summary>
    public int MaxPerRow { get; set; } = 0;

    /// <summary>自由摆放：整排固定在自己拖到的位置，不跟手牌。</summary>
    public bool FreePlace { get; set; } = true;

    /// <summary>自由摆放时的整体缩放（滚轮调整）。</summary>
    public float FreeScale { get; set; } = 1f;

    public float FreeX { get; set; } = 0f;

    public float FreeY { get; set; } = 0f;

    /// <summary>首次进战斗时是否已经套用过「顶部居中」的默认位置。</summary>
    public bool FreePlaceInitialized { get; set; } = false;

    public bool Debug { get; set; } = false;

    /// <summary>旧版 config.json（Enabled / EnergyOrbs / ShowStars / Opacity）先收在这里，Load 后迁移。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Legacy { get; set; }

    /// <summary>把 v0.2.x 的旧键映射到新字段，然后丢掉，避免写回脏键。</summary>
    public void AbsorbLegacy()
    {
        if (Legacy == null || Legacy.Count == 0)
        {
            return;
        }

        bool? enabled = ReadBool("Enabled");
        bool? orbs = ReadBool("EnergyOrbs");
        bool? stars = ReadBool("ShowStars");

        if (enabled == false)
        {
            Mode = OrbDisplayMode.None;
        }
        else if (orbs == false && stars != false)
        {
            Mode = OrbDisplayMode.SecondaryOnly;
        }
        else if (stars == false && orbs != false)
        {
            Mode = OrbDisplayMode.EnergyOnly;
        }

        if (Legacy.TryGetValue("Opacity", out JsonElement op) && op.ValueKind == JsonValueKind.Number)
        {
            float v = Math.Clamp((float)op.GetDouble(), 0.15f, 1f);
            EnergyOpacity = v;
            SecondaryOpacity = v;
        }

        Legacy.Clear();
    }

    private bool? ReadBool(string key)
    {
        if (Legacy == null || !Legacy.TryGetValue(key, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    /// <summary>把手工改坏/旧版本遗留的数值夹回合法区间，避免布局炸掉。</summary>
    public void Sanitize()
    {
        OrbPixels = Math.Clamp(OrbPixels, 24, 96);
        SecondaryPixels = Math.Clamp(SecondaryPixels, 24, 96);
        SecondarySlots = Math.Clamp(SecondarySlots, 0, 12);
        RowOffsetY = Math.Clamp(RowOffsetY, 12, 160);
        RowSpacing = Math.Clamp(RowSpacing, 24, 80);
        RippleScale = Math.Clamp(RippleScale, 1.1f, 4.5f);
        BurstCount = Math.Clamp(BurstCount, 2, 48);
        IdleBobPixels = Math.Clamp(IdleBobPixels, 0, 12);
        EnergyOpacity = Math.Clamp(EnergyOpacity, 0.15f, 1f);
        SecondaryOpacity = Math.Clamp(SecondaryOpacity, 0.15f, 1f);
        MaxPerRow = Math.Clamp(MaxPerRow, 0, 99);
        FreeScale = Math.Clamp(FreeScale, 0.5f, 2.5f);
        if (!Enum.IsDefined(typeof(RowAlign), Align))
        {
            Align = RowAlign.Center;
        }

        if (!Enum.IsDefined(typeof(OrbDisplayMode), Mode))
        {
            Mode = OrbDisplayMode.All;
        }
    }
}

public static class Config
{
    private static Settings _data = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Data => _data;

    /// <summary>整份替换（设置页「恢复默认」用）。</summary>
    public static void Replace(Settings next)
    {
        if (next != null)
        {
            _data = next;
        }
    }

    public static string FilePath { get; private set; }

    public static void Load(string modDir)
    {
        if (FilePath != null)
        {
            return;   // 幂等：设置页注册和启动初始化都会调，只加载一次
        }

        FilePath = Path.Combine(modDir, "hand_pips.json");
        try
        {
            if (File.Exists(FilePath))
            {
                Settings loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options);
                if (loaded != null)
                {
                    _data = loaded;
                }
            }
            else
            {
                Save();
            }
        }
        catch (Exception e)
        {
            // 文件坏了：先备份一份再继续用默认值，避免下一次保存把用户设置整份覆盖
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, FilePath + ".bad", true);
                }
            }
            catch
            {
                // 备份失败就算了，至少下面还有日志
            }

            GD.PushError($"[BangDreamInSpire/handpips] config load failed ({e.Message}); defaults in use, old file kept as .bad");
        }

        _data.AbsorbLegacy();
        _data.Sanitize();
    }

    private static bool _dirty;
    private static double _dirtyAt;

    /// <summary>设置页连续拖动时先记账，0.25 s 后统一落盘，避免每动一格写一次文件。</summary>
    public static void SaveDeferred()
    {
        _dirty = true;
        _dirtyAt = Godot.Time.GetTicksMsec() / 1000.0;
    }

    /// <summary>立刻落盘（非战斗时没有 Tick，用 SceneTree 计时器兜底调用这个）。</summary>
    public static void FlushDeferredNow()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        Save();
    }

    /// <summary>每帧调一次：超过 0.25 s 没有新改动就真正写盘。</summary>
    public static void FlushIfDirty()
    {
        if (!_dirty)
        {
            return;
        }

        if (Godot.Time.GetTicksMsec() / 1000.0 - _dirtyAt < 0.25)
        {
            return;
        }

        _dirty = false;
        Save();
    }

    public static void Save()
    {
        try
        {
            // 先写临时文件再替换：崩溃/断电不会留下半截 JSON
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Options));
            if (File.Exists(FilePath))
            {
                File.Replace(tmp, FilePath, FilePath + ".bak");
            }
            else
            {
                File.Move(tmp, FilePath);
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"[BangDreamInSpire/handpips] config save: {e.Message}");
        }
    }
}
