using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Simple HUD widget showing the current in-game time (24h)
public class ClockHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform root;
    [SerializeField] private Vector2 size = new Vector2(220, 56);
    [SerializeField] private float topMargin = 18f; // distance from top edge

    [Header("Style")]
    [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.22f);
    [SerializeField] private Color textColor = Color.white;

    private TMP_Text tmp;
    private Text uiText;
    private Image bgImg;
    private Canvas ownCanvas;

    private void Awake()
    {
        EnsureUI();
        ApplyLayout();
    }

    private void Update()
    {
        var tod = TimeOfDayManager.Instance;
        string timeStr = tod != null ? tod.GetTimeString24() : "--:--";
        if (tmp != null) tmp.text = timeStr;
        if (uiText != null) uiText.text = timeStr;

        if (tod != null && bgImg != null)
        {
            float t = tod.Day01;
            float a = Mathf.Lerp(0.6f, 0.35f, Mathf.Cos(t * Mathf.PI * 2f) * 0.5f + 0.5f);
            var c = bgColor; c.a = a; bgImg.color = c;
        }
    }

    private void EnsureUI()
    {
        // Create our own overlay canvas at scene root so it's never hidden by parent
        var canvasGO = new GameObject("ClockHUDCanvas", typeof(RectTransform), typeof(Canvas));
        ownCanvas = canvasGO.GetComponent<Canvas>();
        ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ownCanvas.sortingOrder = 3000; // ensure on top
        canvasGO.transform.SetParent(null, false); // root level

        if (root == null)
        {
            var rootGO = new GameObject("ClockRoot", typeof(RectTransform));
            root = rootGO.GetComponent<RectTransform>();
            root.SetParent(ownCanvas.transform, false);
        }

        var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgImg = bgGO.GetComponent<Image>();
        bgRT.SetParent(root, false);
        bgRT.anchorMin = new Vector2(0, 0);
        bgRT.anchorMax = new Vector2(1, 1);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgImg.color = bgColor;
        var outline = bgGO.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.SetParent(root, false);
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 1);
        labelRT.offsetMin = new Vector2(12, 0);
        labelRT.offsetMax = new Vector2(-12, 0);

        tmp = labelGO.AddComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.enableAutoSizing = false;
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.color = textColor;
            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1.25f, -1.25f);
        }
        else
        {
            uiText = labelGO.AddComponent<Text>();
            uiText.resizeTextForBestFit = false;
            uiText.fontSize = 22;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = textColor;
        }
    }

    private void ApplyLayout()
    {
        if (root == null) return;
        // Center-top
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = size;
        root.anchoredPosition = new Vector2(0f, -topMargin);
    }
}
