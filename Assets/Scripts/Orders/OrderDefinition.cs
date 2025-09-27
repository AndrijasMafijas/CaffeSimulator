using UnityEngine;

[CreateAssetMenu(menuName = "Caffe/Order", fileName = "NewOrder")]
public class OrderDefinition : ScriptableObject
{
    public ItemDefinition requiredItem;
    [Min(1)] public int quantity = 1;
    [Min(0)] public int reward = 10;
    [Min(1f)] public float maxWaitSeconds = 30f;

    public string GetLabel()
    {
        if (requiredItem == null) return "Order: (unset)";
        return $"Order: {requiredItem.displayName} x{quantity}";
    }
}
