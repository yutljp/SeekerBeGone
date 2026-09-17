using System;
using UnityEngine;

namespace SeekerBeGone;

// Applied manually from SeekerBeGonePlugin.PatchZsfx, not via [HarmonyPatch].
internal static class ZSFX_Mute_Patch
{
    internal static void Postfix(ZSFX __instance)
    {
        if (SeekerBeGonePlugin.MuteKeywords.Length == 0)
            return;

        var ownName = __instance.gameObject.name.ToLowerInvariant();
        var rootName = __instance.transform.root.gameObject.name.ToLowerInvariant();
        if (!Matches(ownName) && !Matches(rootName))
            return;

        foreach (var source in __instance.GetComponentsInChildren<AudioSource>(includeInactive: true))
            source.mute = true;
    }

    // Sound objects are named like "sfx_seeker_brute_groundslam_impact", so match anywhere in the name.
    private static bool Matches(string name)
    {
        foreach (var keyword in SeekerBeGonePlugin.MuteKeywords)
        {
            if (name.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }
}
