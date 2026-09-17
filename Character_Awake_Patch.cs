using System;
using HarmonyLib;

namespace SeekerBeGone;

[HarmonyPatch(typeof(Character), "Awake")]
internal static class Character_Awake_Patch
{
    private static void Postfix(Character __instance)
    {
        var prefabName = Utils.GetPrefabName(__instance.gameObject);
        if (!SeekerBeGonePlugin.TargetMap.TryGetValue(prefabName, out var scale))
            return;

        try
        {
            Morpher.Morph(__instance, scale);
        }
        catch (Exception e)
        {
            SeekerBeGonePlugin.Log.LogWarning($"Failed to morph {prefabName}: {e}");
        }
    }
}
