#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class InventoryEntryPrefabCreator
{
    [MenuItem("Tools/Caffe/Create Inventory Entry Prefab")]
    public static void CreatePrefab()
    {
        // Create root
        var root = new GameObject("ItemEntry", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();

        // Layout
        var h = root.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.spacing = 8f;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;

        // Icon
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icon = iconGO.GetComponent<Image>();
        var iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.SetParent(rt, false);
        iconRt.sizeDelta = new Vector2(24, 24);
        icon.preserveAspect = true;

        // Label (TMP preferred)
        var labelGO = new GameObject("Label", typeof(RectTransform));
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        TMP_Text tmp = labelGO.AddComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.fontSize = 24;
            tmp.enableAutoSizing = true;
            tmp.alignment = TextAlignmentOptions.Left;
        }

        // Helper component
        var elem = root.AddComponent<InventoryHUDElement>();
        // Assign serialized fields via SerializedObject (they are private)
        var so = new SerializedObject(elem);
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("tmpText").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Ensure folder
        const string folder = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        // Save prefab
        string path = folder + "/ItemEntry.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Inventory Entry", "Created prefab at\n" + path, "OK");
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
}
#endif
