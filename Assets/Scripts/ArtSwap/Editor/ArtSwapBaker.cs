using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor utilities to permanently apply ArtSwap in the scene (bake visuals)
namespace ArtSwapTools
{
    public static class ArtSwapBaker
    {
        [MenuItem("Tools/ArtSwap/Bake Selected", priority = 200)]
        public static void BakeSelectedKeepContainer()
        {
            var swaps = Selection.gameObjects
                .SelectMany(go => go.GetComponentsInChildren<ArtSwap>(true))
                .Distinct()
                .ToArray();
            if (swaps.Length == 0)
            {
                Debug.Log("ArtSwap Baker: No ArtSwap components found in selection.");
                return;
            }

            Undo.SetCurrentGroupName("Bake ArtSwap (Keep Container)");
            var group = Undo.GetCurrentGroup();

            foreach (var swap in swaps)
            {
                BakeOne(swap, flatten: false);
            }

            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/ArtSwap/Bake Selected (Flatten)", priority = 201)]
        public static void BakeSelectedFlatten()
        {
            var swaps = Selection.gameObjects
                .SelectMany(go => go.GetComponentsInChildren<ArtSwap>(true))
                .Distinct()
                .ToArray();
            if (swaps.Length == 0)
            {
                Debug.Log("ArtSwap Baker: No ArtSwap components found in selection.");
                return;
            }

            Undo.SetCurrentGroupName("Bake ArtSwap (Flatten)");
            var group = Undo.GetCurrentGroup();

            foreach (var swap in swaps)
            {
                BakeOne(swap, flatten: true);
            }

            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/ArtSwap/Bake All In Scene", priority = 210)]
        public static void BakeAllInScene()
        {
            var swaps = Object.FindObjectsOfType<ArtSwap>(true);
            if (swaps.Length == 0)
            {
                Debug.Log("ArtSwap Baker: No ArtSwap components found in the scene.");
                return;
            }

            Undo.SetCurrentGroupName("Bake All ArtSwap In Scene");
            var group = Undo.GetCurrentGroup();

            foreach (var swap in swaps)
            {
                BakeOne(swap, flatten: false);
            }

            Undo.CollapseUndoOperations(group);
        }

        private static void BakeOne(ArtSwap swap, bool flatten)
        {
            if (swap == null) return;

            // Keep a reference to GO for SetDirty before destroying components
            var rootGO = swap.gameObject;

            Undo.RegisterFullObjectHierarchyUndo(rootGO, "Bake ArtSwap");

            // Ensure a clean visual state then perform swap in editor context
            swap.RemoveAllVisualsImmediate();
            swap.swapOnStart = false;
            swap.Swap();

            // Optionally flatten the Visual container
            var visualParent = swap.visualParent;
            if (flatten && visualParent != null)
            {
                // Reparent children of Visual to the swap root with Undo and keep world position
                for (int i = visualParent.childCount - 1; i >= 0; i--)
                {
                    var child = visualParent.GetChild(i);
                    Undo.SetTransformParent(child, swap.transform, "Reparent Visual Children");
                    child.SetParent(swap.transform, true);
                }
                // Remove empty Visual object
                Undo.DestroyObjectImmediate(visualParent.gameObject);
            }

            // Mark root dirty so scene is saved
            EditorUtility.SetDirty(rootGO);

            // Remove any component named "ArtSwapMarker" if present
            var monos = rootGO.GetComponents<MonoBehaviour>();
            foreach (var mb in monos)
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type != null && type.Name == "ArtSwapMarker")
                {
                    Undo.DestroyObjectImmediate(mb);
                    break;
                }
            }

            // Remove ArtSwap itself to make the change permanent at edit-time
            Undo.DestroyObjectImmediate(swap);
        }
    }
}
