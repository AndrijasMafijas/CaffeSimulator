using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryHUDElement : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text uiText;

    [Header("Appearance")]
    [SerializeField] private int tmpFontSize = 14; // smaller by default
    [SerializeField] private int uiFontSize = 12;  // smaller by default

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
    }

    private void AutoWire()
    {
        if (icon == null) icon = GetComponentInChildren<Image>(true);
        if (tmpText == null) tmpText = GetComponentInChildren<TMP_Text>(true);
        if (uiText == null) uiText = GetComponentInChildren<Text>(true);
    }

    public void Set(Sprite sprite, string label)
    {
        AutoWire();

        if (icon != null) icon.sprite = sprite;
        if (tmpText != null)
        {
            tmpText.enableAutoSizing = false;
            tmpText.fontSize = tmpFontSize;
            tmpText.alignment = TextAlignmentOptions.Left;
            tmpText.text = label;
        }
        if (uiText != null)
        {
            uiText.resizeTextForBestFit = false;
            uiText.fontSize = uiFontSize;
            uiText.alignment = TextAnchor.MiddleLeft;
            uiText.text = label;
        }
    }
}
