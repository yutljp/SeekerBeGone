using System.Linq;
using UnityEngine;

namespace SeekerBeGone;

// Rewrites item prefabs in place, so ground drops, inventory icons and item stands all pick it up.
// Prefabs are persistent assets: children cannot be added to them, but component fields can be changed.
internal static class ItemMorpher
{
    public static void PatchAll(ObjectDB db)
    {
        if (db.m_items.Count == 0)
            return;

        foreach (var pair in SeekerBeGonePlugin.ItemReplacementMap)
        {
            var target = db.GetItemPrefab(pair.Key);
            var replacement = db.GetItemPrefab(pair.Value);
            if (target == null || replacement == null)
            {
                SeekerBeGonePlugin.Log.LogWarning($"Item replacement {pair.Key} -> {pair.Value}: prefab not found");
                continue;
            }
            Patch(target, replacement);
        }
    }

    private static void Patch(GameObject target, GameObject replacement)
    {
        target.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons =
            replacement.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons;

        // Item stands instantiate only the "attach" child, so the model swap has to happen inside it.
        var srcAttach = replacement.transform.Find("attach");
        var dstAttach = target.transform.Find("attach");
        var srcFilter = FirstMesh(srcAttach);
        var dstFilter = FirstMesh(dstAttach);
        if (srcFilter == null || dstFilter == null)
        {
            SeekerBeGonePlugin.Log.LogWarning($"Item {target.name} -> {replacement.name}: no mesh under 'attach', model not replaced");
            return;
        }

        var dstRenderer = dstFilter.GetComponent<MeshRenderer>();
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = renderer == dstRenderer;
        dstFilter.sharedMesh = srcFilter.sharedMesh;
        dstRenderer.sharedMaterials = srcFilter.GetComponent<MeshRenderer>().sharedMaterials;
        CopyLocalTransform(srcFilter.transform, dstFilter.transform);
        CopyLocalTransform(srcAttach, dstAttach);
    }

    private static MeshFilter FirstMesh(Transform attach) =>
        attach == null
            ? null
            : attach.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.GetComponent<MeshRenderer>() != null);

    private static void CopyLocalTransform(Transform src, Transform dst)
    {
        dst.localPosition = src.localPosition;
        dst.localRotation = src.localRotation;
        dst.localScale = src.localScale;
    }
}
