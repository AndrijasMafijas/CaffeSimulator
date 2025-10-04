using System.Collections.Generic;
using UnityEngine;

// Scriptable catalog of art prefabs you want to use for swapping visuals at runtime
[CreateAssetMenu(menuName = "ArtSwap/Asset Catalog", fileName = "AssetCatalog")]
public class AssetCatalog : ScriptableObject
{
    [Tooltip("Prefabs to use as visual replacements (from Asset Store or your own).")]
    public List<GameObject> prefabs = new List<GameObject>();
}
