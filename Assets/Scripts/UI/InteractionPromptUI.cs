using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    [Header("Assignments")]
    [SerializeField] private GameObject root;              // Container for the prompt (NOT the whole Canvas)
    [SerializeField] private GameObject promptObject;      // The object we toggle on/off (defaults to text object)
    [SerializeField] private Text uiText;                  // Legacy UGUI Text
    [SerializeField] private TMP_Text tmpText;             // TextMeshPro Text

    [Header("Behavior")]
    [Tooltip("If true, toggles the whole root object. If false, toggles only Prompt Object.")]
    [SerializeField] private bool toggleWholeRoot = false; // Keep false if root is your main Canvas

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
        Hide();
    }

    private void AutoWire()
    {
        if (root == null) root = gameObject;
        if (tmpText == null) tmpText = GetComponentInChildren<TMP_Text>(true);
        if (uiText == null) uiText = GetComponentInChildren<Text>(true);
        if (promptObject == null)
        {
            if (tmpText != null) promptObject = tmpText.gameObject;
            else if (uiText != null) promptObject = uiText.gameObject;
            else promptObject = root; // fallback
        }
    }

    public void Show(string message)
    {
        if (tmpText != null) tmpText.text = message;
        if (uiText != null) uiText.text = message;

        if (toggleWholeRoot)
        {
            if (root != null && !root.activeSelf) root.SetActive(true);
        }
        else
        {
            if (promptObject != null && !promptObject.activeSelf) promptObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (toggleWholeRoot)
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }
        else
        {
            if (promptObject != null && promptObject.activeSelf) promptObject.SetActive(false);
        }
    }
}
