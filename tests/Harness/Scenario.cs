using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SeekerBeGone.TestHarness;

[HarmonyPatch(typeof(Player), "OnSpawned")]
internal static class Player_OnSpawned_Patch
{
    private static bool s_ran;

    private static void Postfix(Player __instance)
    {
        if (s_ran)
            return;
        s_ran = true;
        HarnessPlugin.Instance.StartCoroutine(Scenario.Run(__instance));
    }
}

// Spawns every configured target in front of the player, screenshots them alive and dead, logs
// what the morph did to each one, then quits the game.
internal static class Scenario
{
    private const float Distance = 7f;
    private const float Spacing = 2.5f;

    public static IEnumerator Run(Player player)
    {
        yield return new WaitForSeconds(4f);

        player.SetGodMode(true);
        var env = EnvMan.instance;
        env.m_debugTimeOfDay = true;
        env.m_debugTime = 0.5f;
        env.SetForceEnvironment("Clear");

        var origin = player.transform.position;
        var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, forward);

        LogRelatedPrefabs();

        var names = SeekerBeGonePlugin.TargetMap.Keys.ToList();
        var spawned = new List<Character>();
        for (var i = 0; i < names.Count; i++)
        {
            var offset = (i - (names.Count - 1) / 2f) * Spacing;
            var chr = Spawn(names[i], origin + forward * Distance + right * offset, Quaternion.LookRotation(-forward))?.GetComponent<Character>();
            if (chr != null)
                spawned.Add(chr);
        }
        var hideNames = SeekerBeGonePlugin.HideObjectSet.ToList();
        var hidden = new List<GameObject>();
        for (var i = 0; i < hideNames.Count; i++)
        {
            var go = Spawn(hideNames[i], origin + forward * 4f + right * (3f + i * 2f), Quaternion.identity);
            if (go != null)
                hidden.Add(go);
        }

        var itemNames = SeekerBeGonePlugin.ItemReplacementMap.Keys.ToList();
        var items = new List<GameObject>();
        for (var i = 0; i < itemNames.Count; i++)
        {
            var go = Spawn(itemNames[i], origin + forward * 3f + right * (-2f - i * 1.5f), Quaternion.identity);
            if (go != null)
                items.Add(go);
        }

        yield return new WaitForSeconds(1f);
        LogDiagnostics(spawned);
        LogHiddenObjects(hidden);
        LogAttackItems(names);
        LogItems(items);
        yield return Shot("01_spawned", spawned, player);

        for (var i = 0; i < 6; i++)
        {
            yield return new WaitForSeconds(1.5f);
            yield return Shot($"{i + 2:00}_live", spawned, player);
        }

        foreach (var chr in spawned)
        {
            if (chr != null && !chr.IsDead())
                chr.SetHealth(0f);
        }
        yield return new WaitForSeconds(1.5f);
        yield return Shot("08_dead", spawned, player);
        yield return new WaitForSeconds(2f);
        yield return Shot("09_dead", spawned, player);

        LogAudio();
        HarnessPlugin.Log.LogInfo("scenario finished, quitting");
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }

    private static GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
    {
        var prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null)
        {
            HarnessPlugin.Log.LogWarning($"prefab {prefabName} not found");
            return null;
        }
        if (ZoneSystem.instance.GetGroundHeight(pos, out var height))
            pos.y = height + 0.3f;
        var go = Object.Instantiate(prefab, pos, rot);
        HarnessPlugin.Log.LogInfo($"spawned {prefabName} at {pos}");
        return go;
    }

    private static IEnumerator Shot(string name, List<Character> spawned, Player player)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(HarnessPlugin.ShotPath(name));
        HarnessPlugin.Log.LogInfo($"[{name}] " + string.Join(" | ", spawned.Select(c => Describe(c, player))));
        yield return null;
    }

    private static string Describe(Character chr, Player player)
    {
        if (chr == null)
            return "(destroyed)";
        var visual = chr.transform.Find(Morpher.VisualName);
        var src = SourceAnimator(chr, visual);
        var dst = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
        var dist = Vector3.Distance(chr.transform.position, player.transform.position);
        var visualState = visual != null ? visual.gameObject.activeInHierarchy.ToString() : "none";
        return $"{Utils.GetPrefabName(chr.gameObject)}: dist={dist:F1} dead={chr.IsDead()} src={ClipName(src)} dst={ClipName(dst)} visual={visualState}";
    }

    private static void LogDiagnostics(List<Character> spawned)
    {
        foreach (var chr in spawned)
        {
            if (chr == null)
                continue;
            var visual = chr.transform.Find(Morpher.VisualName);
            var src = SourceAnimator(chr, visual);
            var dst = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;

            var sb = new StringBuilder();
            sb.AppendLine($"=== {Utils.GetPrefabName(chr.gameObject)} ===");
            sb.AppendLine(visual == null
                ? "visual: MISSING"
                : $"visual: active={visual.gameObject.activeInHierarchy} scale={visual.localScale} mirror={visual.GetComponent<AnimatorMirror>() != null}");

            var leaks = chr.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && !IsUnder(r.transform, visual))
                .Select(r => r.name).ToList();
            sb.AppendLine($"original renderers still enabled: {leaks.Count} [{string.Join(",", leaks)}]");
            var unmuted = chr.GetComponentsInChildren<AudioSource>(true)
                .Where(a => !a.mute && !IsUnder(a.transform, visual))
                .Select(a => a.name).ToList();
            sb.AppendLine($"original audio sources unmuted: {unmuted.Count} [{string.Join(",", unmuted)}]");

            sb.AppendLine($"src params: {Params(src)}");
            sb.AppendLine($"dst params: {Params(dst)}");
            if (src != null && dst != null)
            {
                var shared = dst.parameters
                    .Where(p => p.type != AnimatorControllerParameterType.Trigger
                                && src.parameters.Any(s => s.nameHash == p.nameHash && s.type == p.type))
                    .Select(p => p.name);
                sb.AppendLine($"shared params: [{string.Join(",", shared)}]");
                var triggers = dst.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name);
                sb.AppendLine($"dst triggers: [{string.Join(",", triggers)}]");
            }

            sb.AppendLine($"death effects: [{Effects(chr.m_deathEffects)}] hit effects: [{Effects(chr.m_hitEffects)}]");
            var ai = chr.GetComponent<BaseAI>();
            if (ai != null)
                sb.AppendLine($"idle sound: [{Effects(ai.m_idleSound)}] alerted: [{Effects(ai.m_alertedEffects)}]");
            var footStep = chr.GetComponentInChildren<FootStep>(true);
            if (footStep != null)
                sb.AppendLine($"footstep effects: {footStep.m_effects?.Count}");

            HarnessPlugin.Log.LogInfo(sb.ToString());
        }
    }

    private static void LogRelatedPrefabs()
    {
        var keys = new[] { "seeker", "tick", "queen", "hive" };
        var names = ZNetScene.instance.GetPrefabNames()
            .Where(n => keys.Any(k => n.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(n => n);
        HarnessPlugin.Log.LogInfo($"related prefabs: [{string.Join(",", names)}]");
    }

    private static void LogHiddenObjects(List<GameObject> hidden)
    {
        foreach (var go in hidden)
        {
            if (go == null)
                continue;
            var enabled = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).Select(r => r.name).ToList();
            HarnessPlugin.Log.LogInfo($"hidden {Utils.GetPrefabName(go)}: renderers still enabled: {enabled.Count} [{string.Join(",", enabled)}]");
        }
    }

    // Shows which attack items each target actually carries and what effects they now reference
    // (item-level list | attack-level list).
    private static void LogAttackItems(List<string> prefabNames)
    {
        foreach (var name in prefabNames.Append(SeekerBeGonePlugin.ReplacementCreature.Value))
        {
            var humanoid = ZNetScene.instance.GetPrefab(name)?.GetComponent<Humanoid>();
            if (humanoid == null)
            {
                HarnessPlugin.Log.LogInfo($"attack items {name}: not a Humanoid");
                continue;
            }
            var lines = new List<string>();
            foreach (var item in Morpher.AllItems(humanoid))
            {
                var shared = item?.GetComponent<ItemDrop>()?.m_itemData.m_shared;
                if (shared?.m_attack == null)
                    continue;
                var a = shared.m_attack;
                lines.Add($"{item.name}: start=[{Effects(shared.m_startEffect)}|{Effects(a.m_startEffect)}] trigger=[{Effects(shared.m_triggerEffect)}|{Effects(a.m_triggerEffect)}] hit=[{Effects(shared.m_hitEffect)}|{Effects(a.m_hitEffect)}] terrain=[{Effects(shared.m_hitTerrainEffect)}|{Effects(a.m_hitTerrainEffect)}]");
            }
            HarnessPlugin.Log.LogInfo($"attack items {name}: default={humanoid.m_defaultItems.Length} weapon={humanoid.m_randomWeapon.Length} sets={humanoid.m_randomSets.Length}\n{string.Join("\n", lines)}");
        }
    }

    private static void LogItems(List<GameObject> items)
    {
        foreach (var go in items)
        {
            if (go == null)
                continue;
            var name = Utils.GetPrefabName(go);
            var icons = go.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons;
            var icon = icons.Length > 0 && icons[0] != null ? icons[0].name : "-";
            HarnessPlugin.Log.LogInfo($"item {name}: icon={icon}\n{Hierarchy(go.transform, "")}");
        }
        foreach (var replacement in SeekerBeGonePlugin.ItemReplacementMap.Values.Distinct())
        {
            var prefab = ZNetScene.instance.GetPrefab(replacement);
            if (prefab != null)
                HarnessPlugin.Log.LogInfo($"replacement item prefab {replacement}:\n{Hierarchy(prefab.transform, "")}");
        }
    }

    private static string Hierarchy(Transform t, string indent)
    {
        var renderer = t.GetComponent<Renderer>();
        var state = renderer != null ? (renderer.enabled ? " [renderer on]" : " [renderer off]") : "";
        var mesh = t.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh != null)
            state += $" mesh={mesh.name} pos={t.localPosition} scale={t.localScale}";
        var sb = new StringBuilder($"{indent}{t.name}{state}\n");
        foreach (Transform child in t)
            sb.Append(Hierarchy(child, indent + "  "));
        return sb.ToString();
    }

    private static void LogAudio()
    {
        var all = Object.FindObjectsByType<ZSFX>(FindObjectsSortMode.None);
        var muted = new List<string>();
        var unmuted = new List<string>();
        foreach (var sfx in all)
        {
            var source = sfx.GetComponent<AudioSource>();
            if (source == null)
                continue;
            (source.mute ? muted : unmuted).Add(sfx.name);
        }
        HarnessPlugin.Log.LogInfo($"ZSFX muted ({muted.Count}): [{string.Join(",", muted.Distinct().OrderBy(n => n))}]");
        HarnessPlugin.Log.LogInfo($"ZSFX unmuted ({unmuted.Count}): [{string.Join(",", unmuted.Distinct().OrderBy(n => n))}]");
    }

    private static bool IsUnder(Transform t, Transform root) => root != null && t.IsChildOf(root);

    private static Animator SourceAnimator(Character chr, Transform visual) =>
        chr.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => !IsUnder(a.transform, visual));

    private static string ClipName(Animator anim)
    {
        if (anim == null)
            return "-";
        var clips = anim.GetCurrentAnimatorClipInfo(0);
        return clips.Length > 0 ? clips[0].clip.name : "?";
    }

    private static string Params(Animator anim) =>
        anim == null ? "-" : string.Join(",", anim.parameters.Select(p => $"{p.name}:{p.type}"));

    private static string Effects(EffectList list) =>
        list?.m_effectPrefabs == null ? "-" : string.Join(",", list.m_effectPrefabs.Select(e => e.m_prefab != null ? e.m_prefab.name : "null"));
}
