using HarmonyLib;

namespace SeekerBeGone;

// ObjectDB is recreated per scene (and filled via CopyOtherDB in the menu scene), but the item prefabs
// it lists are shared assets, so patching them repeatedly is harmless.
[HarmonyPatch(typeof(ObjectDB))]
internal static class ObjectDB_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    private static void Awake_Postfix(ObjectDB __instance) => PatchItems(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ObjectDB.CopyOtherDB))]
    private static void CopyOtherDB_Postfix(ObjectDB __instance) => PatchItems(__instance);

    private static void PatchItems(ObjectDB db)
    {
        if (SeekerBeGonePlugin.ItemReplacementMap.Count > 0)
            ItemMorpher.PatchAll(db);
    }
}
