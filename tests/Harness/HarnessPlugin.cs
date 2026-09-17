using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SeekerBeGone.TestHarness;

// Inert unless the game is launched with "-sbgtest <outdir>"; see tests/run-harness.ps1.
[BepInPlugin(PluginGuid, "SeekerBeGone TestHarness", "0.1.0")]
[BepInDependency(SeekerBeGonePlugin.PluginGuid)]
public class HarnessPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "yutljp.seekerbegone.testharness";
    internal const string WorldName = "SBGTest";
    internal const string ProfileName = "sbgtest";

    internal static HarnessPlugin Instance;
    internal static ManualLogSource Log;
    internal static string OutDir;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "-sbgtest");
        if (i < 0 || i + 1 >= args.Length)
        {
            Log.LogInfo("-sbgtest <outdir> not given, harness inactive");
            return;
        }

        OutDir = Path.GetFullPath(args[i + 1]);
        // Keep the throwaway world and character out of the real save folder.
        var saveDir = Path.Combine(OutDir, "savedir");
        Directory.CreateDirectory(saveDir);
        Utils.SetSaveDataPath(saveDir);

        new Harmony(PluginGuid).PatchAll();
        Log.LogInfo($"harness active, output: {OutDir}");
    }

    // AudioMan.Start resets the listener volume once; keep it silent for the whole run.
    private void LateUpdate()
    {
        if (OutDir != null)
            AudioListener.volume = 0f;
    }

    internal static string ShotPath(string name) => Path.Combine(OutDir, name + ".png");
}
