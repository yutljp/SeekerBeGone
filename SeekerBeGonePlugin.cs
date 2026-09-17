using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SeekerBeGone;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class SeekerBeGonePlugin : BaseUnityPlugin
{
    public const string PluginGuid = "yutljp.seekerbegone";
    public const string PluginName = "SeekerBeGone";
    public const string PluginVersion = "1.2.0";

    internal static ManualLogSource Log;

    internal static ConfigEntry<string> ReplacementCreature;
    internal static ConfigEntry<string> Targets;
    internal static ConfigEntry<bool> ReplaceSounds;
    internal static ConfigEntry<string> MuteSoundKeywords;
    internal static ConfigEntry<string> HideObjects;
    internal static ConfigEntry<string> ItemReplacements;

    internal static readonly Dictionary<string, float> TargetMap = new();
    internal static string[] MuteKeywords = Array.Empty<string>();
    internal static readonly HashSet<string> HideObjectSet = new();
    internal static readonly Dictionary<string, string> ItemReplacementMap = new();

    private void Awake()
    {
        Log = Logger;

        ReplacementCreature = Config.Bind("General", "ReplacementCreature", "Wolf",
            "見た目と音の置き換え元にするクリーチャーのプレハブ名 (例: Wolf, Boar, Neck)");
        Targets = Config.Bind("General", "Targets",
            "Seeker:1.5,SeekerBrute:2.2,SeekerBrood:0.6,SeekerQueen:3.0,Tick:0.4",
            "置き換える敵の「プレハブ名:見た目のスケール」をカンマ区切りで指定。Gjall:2.5 のように追加も可能");
        ReplaceSounds = Config.Bind("General", "ReplaceSounds", true,
            "鳴き声・足音・攻撃音などを置き換え元クリーチャーのものに差し替える");
        MuteSoundKeywords = Config.Bind("General", "MuteSoundKeywords", "seeker,hivequeen,tick",
            "この文字列を名前に含む効果音オブジェクトをすべてミュートする (経路を問わない安全網)。空にすると無効");
        HideObjects = Config.Bind("General", "HideObjects", "SeekerEgg,SeekerEgg_alwayshatch",
            "描画を消す環境オブジェクトのプレハブ名 (坑道の卵嚢など)。当たり判定と採取は可能なまま。空にすると無効");
        ItemReplacements = Config.Bind("General", "ItemReplacements",
            "TrophySeeker:TrophyWolf,TrophySeekerBrute:TrophyWolf,TrophySeekerQueen:TrophyWolf,TrophyTick:TrophyWolf",
            "見た目とアイコンを差し替えるアイテムを「元のプレハブ名:置き換え先のプレハブ名」のカンマ区切りで指定 (トロフィーなど)。空にすると無効");

        ParseConfig();

        var harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        PatchZsfx(harmony);

        Log.LogInfo($"{PluginName} {PluginVersion} loaded. {Targets.Value} -> {ReplacementCreature.Value}");
    }

    private static void ParseConfig()
    {
        TargetMap.Clear();
        foreach (var (name, scale) in SplitPairs(Targets.Value))
            TargetMap[name] = float.TryParse(scale, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1f;

        MuteKeywords = SplitList(MuteSoundKeywords.Value).Select(k => k.ToLowerInvariant()).ToArray();

        HideObjectSet.Clear();
        HideObjectSet.UnionWith(SplitList(HideObjects.Value));

        ItemReplacementMap.Clear();
        foreach (var (source, replacement) in SplitPairs(ItemReplacements.Value))
        {
            if (replacement.Length > 0)
                ItemReplacementMap[source] = replacement;
        }
    }

    // "a, b,c" -> a, b, c
    private static IEnumerable<string> SplitList(string value) =>
        value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

    // "a:1, b" -> (a, 1), (b, "")
    private static IEnumerable<(string, string)> SplitPairs(string value) =>
        SplitList(value)
            .Select(entry => entry.Split(':'))
            .Select(parts => (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : ""))
            .Where(pair => pair.Item1.Length > 0);

    // ZSFX's entry point differs between game versions, so resolve it at runtime instead of via attributes.
    private static void PatchZsfx(Harmony harmony)
    {
        if (MuteKeywords.Length == 0)
            return;

        var postfix = new HarmonyMethod(typeof(ZSFX_Mute_Patch), nameof(ZSFX_Mute_Patch.Postfix));
        foreach (var methodName in new[] { "Awake", "Play" })
        {
            var method = AccessTools.Method(typeof(ZSFX), methodName);
            if (method == null)
                continue;
            harmony.Patch(method, postfix: postfix);
            return;
        }

        Log.LogWarning("ZSFX.Awake/Play が見つからないため効果音ミュートの安全網は無効です");
    }
}
