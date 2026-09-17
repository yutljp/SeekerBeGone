using HarmonyLib;

namespace SeekerBeGone;

[HarmonyPatch(typeof(ZNetView), "Awake")]
internal static class ZNetView_Awake_Patch
{
    private static void Postfix(ZNetView __instance)
    {
        if (SeekerBeGonePlugin.HideObjectSet.Count == 0)
            return;

        if (SeekerBeGonePlugin.HideObjectSet.Contains(Utils.GetPrefabName(__instance.gameObject)))
            Morpher.Hide(__instance);
    }
}
