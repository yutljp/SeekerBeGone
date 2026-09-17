using System.Collections.Generic;
using UnityEngine;

namespace SeekerBeGone;

internal static class Morpher
{
    internal const string VisualName = "SeekerBeGone_Visual";

    private static readonly HashSet<string> s_attackEffectsPatched = new();

    public static void Morph(Character chr, float scale)
    {
        var zns = ZNetScene.instance;
        if (zns == null)
            return;

        var replacement = zns.GetPrefab(SeekerBeGonePlugin.ReplacementCreature.Value);
        if (replacement == null)
        {
            SeekerBeGonePlugin.Log.LogWarning($"Replacement prefab '{SeekerBeGonePlugin.ReplacementCreature.Value}' not found");
            return;
        }

        var sourceAnim = chr.GetComponentInChildren<Animator>(includeInactive: true);
        Hide(chr);
        var visual = AttachReplacementVisual(chr, replacement, scale, sourceAnim);
        CopyDeathEffects(chr, replacement);

        if (SeekerBeGonePlugin.ReplaceSounds.Value)
        {
            CopyCharacterEffects(chr, replacement);
            PatchAttackEffects(chr, zns, replacement);
        }

        SeekerBeGonePlugin.Log.LogDebug($"Morphed {chr.gameObject.name} (visual={visual != null})");
    }

    // Disables rendering and audio but leaves colliders, AI and interaction intact.
    internal static void Hide(Component root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(includeInactive: true))
            renderer.enabled = false;
        foreach (var source in root.GetComponentsInChildren<AudioSource>(includeInactive: true))
            source.mute = true;
    }

    private static GameObject AttachReplacementVisual(Character chr, GameObject srcPrefab, float scale, Animator targetAnim)
    {
        // Character.m_visual is not accessible, so locate the visual subtree by convention.
        var visualSrc = srcPrefab.transform.Find("Visual")?.gameObject;
        if (visualSrc == null)
            visualSrc = srcPrefab.GetComponentInChildren<Animator>(includeInactive: true)?.gameObject;
        if (visualSrc == null)
            return null;

        var visual = Object.Instantiate(visualSrc, chr.transform);
        visual.name = VisualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = visualSrc.transform.localScale * scale;

        // The clone must not fire the replacement creature's animation events or sounds on the host.
        // Awake can run inside physics/animation callbacks (egg hatching), where DestroyImmediate is rejected.
        foreach (var animEvent in visual.GetComponentsInChildren<CharacterAnimEvent>(includeInactive: true))
            Object.Destroy(animEvent);
        foreach (var source in visual.GetComponentsInChildren<AudioSource>(includeInactive: true))
            Object.Destroy(source);

        visual.SetActive(true);

        var visualAnim = visual.GetComponentInChildren<Animator>(includeInactive: true);
        if (visualAnim != null && targetAnim != null)
            visual.AddComponent<AnimatorMirror>().Setup(targetAnim, visualAnim);

        return visual;
    }

    // The death effect list carries the ragdoll (the corpse model), so it is swapped even when sounds are kept.
    private static void CopyDeathEffects(Character chr, GameObject srcPrefab)
    {
        var srcChr = srcPrefab.GetComponent<Character>();
        if (srcChr != null)
            chr.m_deathEffects = srcChr.m_deathEffects;
    }

    private static void CopyCharacterEffects(Character chr, GameObject srcPrefab)
    {
        var srcChr = srcPrefab.GetComponent<Character>();
        if (srcChr != null)
        {
            chr.m_hitEffects = srcChr.m_hitEffects;
            chr.m_critHitEffects = srcChr.m_critHitEffects;
            chr.m_backstabHitEffects = srcChr.m_backstabHitEffects;
            chr.m_jumpEffects = srcChr.m_jumpEffects;
            chr.m_flyingContinuousEffect = srcChr.m_flyingContinuousEffect;
        }

        var ai = chr.GetComponent<BaseAI>();
        var srcAi = srcPrefab.GetComponent<BaseAI>();
        if (ai != null && srcAi != null)
        {
            ai.m_alertedEffects = srcAi.m_alertedEffects;
            ai.m_idleSound = srcAi.m_idleSound;
            ai.m_idleSoundChance = srcAi.m_idleSoundChance;
            ai.m_idleSoundInterval = srcAi.m_idleSoundInterval;
        }

        var footStep = chr.GetComponentInChildren<FootStep>(includeInactive: true);
        var srcFootStep = srcPrefab.GetComponentInChildren<FootStep>(includeInactive: true);
        if (footStep != null && srcFootStep != null)
            footStep.m_effects = srcFootStep.m_effects;
    }

    // Attack effects live in shared item data, so patch each target prefab once rather than per instance.
    private static void PatchAttackEffects(Character chr, ZNetScene zns, GameObject srcPrefab)
    {
        var prefabName = Utils.GetPrefabName(chr.gameObject);
        if (!s_attackEffectsPatched.Add(prefabName))
            return;

        var targetHumanoid = zns.GetPrefab(prefabName)?.GetComponent<Humanoid>();
        var srcHumanoid = srcPrefab.GetComponent<Humanoid>();
        if (targetHumanoid == null || srcHumanoid == null)
            return;

        var srcItems = CollectAttackItems(srcHumanoid);
        if (srcItems.Count == 0)
            return;

        var i = 0;
        foreach (var item in CollectAttackItems(targetHumanoid))
            CopyAttackEffects(item, srcItems[i++ % srcItems.Count]);
    }

    private static List<ItemDrop.ItemData.SharedData> CollectAttackItems(Humanoid humanoid)
    {
        var items = new List<ItemDrop.ItemData.SharedData>();
        foreach (var item in AllItems(humanoid))
        {
            var shared = item?.GetComponent<ItemDrop>()?.m_itemData.m_shared;
            if (shared?.m_attack != null)
                items.Add(shared);
        }
        return items;
    }

    // Attack fires both the item-level and the attack-level lists; creature sounds usually sit on the item.
    private static void CopyAttackEffects(ItemDrop.ItemData.SharedData dst, ItemDrop.ItemData.SharedData src)
    {
        dst.m_startEffect = src.m_startEffect;
        dst.m_triggerEffect = src.m_triggerEffect;
        dst.m_hitEffect = src.m_hitEffect;
        dst.m_hitTerrainEffect = src.m_hitTerrainEffect;
        dst.m_trailStartEffect = src.m_trailStartEffect;
        dst.m_attack.m_startEffect = src.m_attack.m_startEffect;
        dst.m_attack.m_triggerEffect = src.m_attack.m_triggerEffect;
        dst.m_attack.m_hitEffect = src.m_attack.m_hitEffect;
        dst.m_attack.m_hitTerrainEffect = src.m_attack.m_hitTerrainEffect;
        dst.m_attack.m_trailStartEffect = src.m_attack.m_trailStartEffect;
    }

    // Creatures get their weapons from any of these lists (Humanoid.GiveDefaultItems), so cover all of them.
    internal static IEnumerable<GameObject> AllItems(Humanoid humanoid)
    {
        foreach (var item in humanoid.m_defaultItems) yield return item;
        foreach (var item in humanoid.m_randomWeapon) yield return item;
        foreach (var item in humanoid.m_randomArmor) yield return item;
        foreach (var item in humanoid.m_randomShield) yield return item;
        foreach (var set in humanoid.m_randomSets)
        {
            foreach (var item in set.m_items) yield return item;
        }
    }
}
