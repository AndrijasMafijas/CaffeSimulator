using UnityEditor;
using UnityEngine;
using System.IO;

// Editor utility to create a Customer prefab from an existing model prefab (e.g. HumanMale_Character_FREE)
// Place this script in Assets/Editor and open it from the "Tools/Create Customer Prefab" menu.
public class CreateCustomerPrefabFromBlink : EditorWindow
{
    private GameObject sourcePrefab;
    private string destinationPath = "Assets/Prefabs/Customer_HumanMale.prefab";
    private bool overwriteExisting = true;

    [MenuItem("Tools/Create Customer Prefab")]
    public static void ShowWindow()
    {
        var w = GetWindow<CreateCustomerPrefabFromBlink>(true, "Create Customer Prefab");
        w.minSize = new Vector2(420, 120);
    }

    private void OnGUI()
    {
        GUILayout.Label("Create a Customer prefab from an existing model prefab", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab (project)", sourcePrefab, typeof(GameObject), false);
        destinationPath = EditorGUILayout.TextField("Destination Path", destinationPath);
        overwriteExisting = EditorGUILayout.ToggleLeft("Overwrite if exists", overwriteExisting);

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Customer Prefab"))
        {
            CreatePrefabFromSource();
        }
    }

    private void CreatePrefabFromSource()
    {
        if (sourcePrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a source prefab from the Project window (e.g. HumanMale_Character_FREE).", "OK");
            return;
        }

        var absDest = destinationPath;
        var destDir = Path.GetDirectoryName(absDest);
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

        if (File.Exists(absDest) && !overwriteExisting)
        {
            EditorUtility.DisplayDialog("Cancelled", "Destination prefab already exists and overwrite is disabled.", "OK");
            return;
        }

        // Instantiate a temporary instance in memory
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        if (temp == null) temp = Instantiate(sourcePrefab);

        // Ensure root has Customer component
        var cust = temp.GetComponent<Customer>();
        if (cust == null)
        {
            cust = temp.AddComponent<Customer>();
        }

        // Ensure physics components present similar to runtime expectations
        var rb = temp.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = temp.AddComponent<Rigidbody>();
        }
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        var col = temp.GetComponent<Collider>();
        if (col == null)
        {
            var cap = temp.AddComponent<CapsuleCollider>();
            cap.height = 2f;
            cap.radius = 0.4f;
            cap.center = new Vector3(0, 1f, 0);
        }

        // If the source prefab already contains an Animator, keep it. Otherwise try to find one in children.
        // (No changes needed here; Customer script picks up animator from children at runtime.)

        // Save as new prefab asset
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(absDest);
        if (existing != null && overwriteExisting)
        {
            AssetDatabase.DeleteAsset(absDest);
        }

        var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(temp, absDest, InteractionMode.UserAction);
        if (saved != null)
        {
            EditorUtility.DisplayDialog("Success", $"Customer prefab saved to {absDest}", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Failed to save prefab. See console for details.", "OK");
        }

        // Clean up temp instance
        if (Application.isEditor) DestroyImmediate(temp);
        else Destroy(temp);

        AssetDatabase.Refresh();
    }
}
