using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Helper editor for quick swapping in the Inspector
public class ArtSwapMarker : MonoBehaviour
{
    public AssetCatalog catalog;
    public int index;
}

#if UNITY_EDITOR
[CustomEditor(typeof(ArtSwapMarker))]
public class ArtSwapMarkerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var m = (ArtSwapMarker)target;
        if (m.catalog == null) return;
        if (GUILayout.Button("Swap Here"))
        {
            var swap = m.GetComponent<ArtSwap>();
            if (swap == null) swap = m.gameObject.AddComponent<ArtSwap>();
            swap.catalog = m.catalog;
            swap.catalogIndex = Mathf.Clamp(m.index, 0, m.catalog.prefabs.Count - 1);
            swap.swapOnStart = false;
            swap.RemoveAllVisualsImmediate();
            swap.Swap();
        }
    }
}
#endif
