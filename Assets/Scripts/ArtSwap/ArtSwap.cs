using System;
using UnityEngine;

// Put this on your placeholder primitives. It will replace the primitive
// with a prefab instance from your AssetCatalog while keeping transform,
// and optionally keeping collider/navmesh obstacles.
public class ArtSwap : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Catalog with prefabs (from Asset Store) to use as replacements.")]
    public AssetCatalog catalog;
    [Tooltip("Index into the catalog list (0-based)")]
    public int catalogIndex = 0;

    [Header("Options")]
    [Tooltip("Swap on Start() automatically")] public bool swapOnStart = true;
    [Tooltip("Parent the spawned art under this object (keeps position/rotation/scale at zero/identity)")]
    public Transform visualParent; // optional child to hold visuals
    [Tooltip("Destroy the original renderer of this object after swap")] public bool removeOriginalRenderer = true;
    [Tooltip("Copy collider from original (Box/Sphere/Capsule/Mesh) to new visual root if missing")] public bool copyColliderIfMissing = true;

    private GameObject spawned;

    private void Reset()
    {
        // Create a child for visuals for tidier hierarchy
        if (visualParent == null)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            visualParent = go.transform;
        }
    }

    private void Start()
    {
        if (swapOnStart) Swap();
    }

    public void Swap()
    {
        if (catalog == null || catalog.prefabs == null || catalog.prefabs.Count == 0)
        {
            Debug.LogWarning($"[{name}] ArtSwap: Catalog is empty");
            return;
        }
        int idx = Mathf.Clamp(catalogIndex, 0, catalog.prefabs.Count - 1);
        var prefab = catalog.prefabs[idx];
        if (prefab == null)
        {
            Debug.LogWarning($"[{name}] ArtSwap: Prefab at index {idx} is null");
            return;
        }

        if (visualParent == null)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            visualParent = go.transform;
        }

        // Remove previous
        if (spawned != null)
        {
            if (Application.isPlaying) Destroy(spawned); else DestroyImmediate(spawned);
        }

        // Spawn new visual
        spawned = Instantiate(prefab, visualParent);
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.identity;
        spawned.transform.localScale = Vector3.one;

        // Optionally remove original primitive renderer
        if (removeOriginalRenderer)
        {
            foreach (var r in GetComponents<Renderer>())
            {
                if (r == null) continue;
                // Don't remove renderers inside visualParent
                if (r.transform.IsChildOf(visualParent)) continue;
                DestroyRenderer(r);
            }
        }

        // Optionally copy collider to the visual root if it doesn't have one
        if (copyColliderIfMissing)
        {
            var newHasCollider = spawned.GetComponent<Collider>() != null || spawned.GetComponentInChildren<Collider>() != null;
            if (!newHasCollider)
            {
                var srcCol = GetComponent<Collider>();
                if (srcCol != null)
                {
                    CopyCollider(srcCol, spawned);
                }
            }
        }
    }

    public void RemoveAllVisualsImmediate()
    {
        if (visualParent == null) return;
        for (int i = visualParent.childCount - 1; i >= 0; i--)
        {
            var c = visualParent.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
        spawned = null;
    }

    private void DestroyRenderer(Renderer r)
    {
        if (Application.isPlaying) Destroy(r); else DestroyImmediate(r);
    }

    private void CopyCollider(Collider src, GameObject dst)
    {
        if (src is BoxCollider bc)
        {
            var c = dst.AddComponent<BoxCollider>();
            c.center = bc.center; c.size = bc.size; c.isTrigger = bc.isTrigger;
        }
        else if (src is SphereCollider sc)
        {
            var c = dst.AddComponent<SphereCollider>();
            c.center = sc.center; c.radius = sc.radius; c.isTrigger = sc.isTrigger;
        }
        else if (src is CapsuleCollider cc)
        {
            var c = dst.AddComponent<CapsuleCollider>();
            c.center = cc.center; c.radius = cc.radius; c.height = cc.height; c.direction = cc.direction; c.isTrigger = cc.isTrigger;
        }
        else if (src is MeshCollider mc)
        {
            var c = dst.AddComponent<MeshCollider>();
            c.sharedMesh = mc.sharedMesh; c.convex = mc.convex; c.isTrigger = mc.isTrigger;
        }
    }
}
