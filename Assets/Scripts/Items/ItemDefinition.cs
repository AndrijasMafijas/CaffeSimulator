using UnityEngine;

[CreateAssetMenu(menuName = "Caffe/Item", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    [Min(1)] public int maxStack = 99;

    [Header("Economy")]
    [Min(0)] public int sellPrice = 0; // 0 = not sellable
}
