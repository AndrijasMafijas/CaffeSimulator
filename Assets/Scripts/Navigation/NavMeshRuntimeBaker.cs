using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Runtime NavMesh rebaker that works even if AI Navigation package is absent (uses reflection)
public class NavMeshRuntimeBaker : MonoBehaviour
{
    private static NavMeshRuntimeBaker _instance;
    private static readonly List<Component> _surfaces = new List<Component>();
    private static MethodInfo _buildMethod;
    private static bool _pending;

    public static void RequestRebake(float delaySeconds = 0.1f)
    {
        EnsureInstance();
        _instance.StartCoroutine(_instance.RebakeRoutine(delaySeconds));
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("NavMeshRuntimeBaker");
        go.hideFlags = HideFlags.HideAndDontSave;
        _instance = go.AddComponent<NavMeshRuntimeBaker>();
        DontDestroyOnLoad(go);
        CacheSurfaces();
    }

    private static void CacheSurfaces()
    {
        _surfaces.Clear();
        _buildMethod = null;

        // Find all components in scene and pick those with type name "NavMeshSurface"
        var all = FindObjectsOfType<Component>(true);
        foreach (var c in all)
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t.Name == "NavMeshSurface")
            {
                _surfaces.Add(c);
                if (_buildMethod == null)
                {
                    _buildMethod = t.GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
        }

#if UNITY_EDITOR
        if (_surfaces.Count == 0)
        {
            Debug.Log("NavMeshRuntimeBaker: No NavMeshSurface found. Install AI Navigation package and add NavMeshSurface to your level root if you want runtime rebake.");
        }
#endif
    }

    private IEnumerator RebakeRoutine(float delay)
    {
        if (_pending) yield break;
        _pending = true;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (_surfaces.Count == 0 || _buildMethod == null)
        {
            CacheSurfaces();
        }

        for (int i = 0; i < _surfaces.Count; i++)
        {
            var s = _surfaces[i];
            if (s == null) continue;
            try
            {
                _buildMethod?.Invoke(s, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NavMeshRuntimeBaker: Failed to build navmesh on {s.name}: {e.Message}");
            }
            // small yield to spread work if multiple surfaces
            yield return null;
        }

        _pending = false;
    }
}
