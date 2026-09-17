using System.Collections;
using HarmonyLib;

namespace SeekerBeGone.TestHarness;

// Bypasses the main menu: selects the throwaway profile and world and loads the main scene.
[HarmonyPatch(typeof(FejdStartup), "Start")]
internal static class FejdStartup_Start_Patch
{
    private static bool s_started;

    private static void Postfix(FejdStartup __instance)
    {
        if (s_started)
            return;
        s_started = true;
        __instance.StartCoroutine(StartWorld(__instance));
    }

    private static IEnumerator StartWorld(FejdStartup fejd)
    {
        yield return null;
        HarnessPlugin.Log.LogInfo($"auto-starting world {HarnessPlugin.WorldName} as {HarnessPlugin.ProfileName}");
        Game.SetProfile(HarnessPlugin.ProfileName, FileHelpers.FileSource.Local);
        var world = World.GetCreateWorld(HarnessPlugin.WorldName, FileHelpers.FileSource.Local);
        ZNet.m_onlineBackend = OnlineBackendType.Steamworks;
        ZSteamMatchmaking.instance.StopServerListing();
        ZNet.SetServer(server: true, openServer: false, publicServer: false, HarnessPlugin.WorldName, "", world);
        ZNet.ResetServerHost();
        SystemResourceManager.FastLoadScene(fejd.m_mainScene);
    }
}

// A fresh profile would otherwise trigger the intro text and the valkyrie flight.
[HarmonyPatch(typeof(Game), "Awake")]
internal static class Game_Awake_Patch
{
    private static void Postfix(Game __instance)
    {
        var profile = __instance.GetPlayerProfile();
        if (profile != null)
            profile.m_firstSpawn = false;
    }
}
