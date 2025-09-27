using UnityEngine;
using UnityEngine.UI;
using TMPro;

// World-space progress UI that follows a target and faces the camera.
public class StationProgressUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform anchor; // what to follow (defaults to this.transform)
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    [Header("UI Elements")]
    [SerializeField] private Canvas worldCanvas; // world-space canvas
    [SerializeField] private RectTransform root; // root rect under canvas
    [SerializeField] private Image barFillImg;   // visual fill image (we stretch via anchors)
    [SerializeField] private TMP_Text tmpLabel;
    [SerializeField] private Text uiLabel;

    [Header("Style")] 
    [SerializeField] private Vector2 size = new Vector2(1.2f, 0.2f); // in world units
    [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color fillColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

    private Camera cam;
    private RectTransform barBgRect;
    private RectTransform barFillRect;

    // runtime 1x1 white sprite to ensure Images render
    private static Sprite sWhiteSprite;

    private void Awake()
    {
        cam = Camera.main;
        if (anchor == null) anchor = transform;
        EnsureWhiteSprite();
        EnsureUI();
        Hide();
    }

    private void OnEnable()
    {
        if (worldCanvas == null || root == null || barFillRect == null)
        {
            EnsureUI();
        }
    }

    private void LateUpdate()
    {
        if (worldCanvas == null) return;
        // follow anchor
        worldCanvas.transform.position = anchor.position + worldOffset;
        // face camera (billboard)
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            var fwd = worldCanvas.transform.position - cam.transform.position;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                worldCanvas.transform.rotation = Quaternion.LookRotation(fwd);
            }
        }
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (barFillRect != null)
        {
            // Stretch fill by moving its right anchor. Left stays at 0.
            var aMin = barFillRect.anchorMin;
            var aMax = barFillRect.anchorMax;
            aMin.x = 0f;
            aMax.x = t;
            barFillRect.anchorMin = aMin;
            barFillRect.anchorMax = aMax;
            // No padding in world space to avoid disappearing due to units scale
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;
        }
    }

    public void SetStatus(string text)
    {
        if (tmpLabel != null) tmpLabel.text = text;
        if (uiLabel != null) uiLabel.text = text;
    }

    public void Show()
    {
        if (worldCanvas != null && !worldCanvas.gameObject.activeSelf)
            worldCanvas.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (worldCanvas != null && worldCanvas.gameObject.activeSelf)
            worldCanvas.gameObject.SetActive(false);
    }

    private void EnsureWhiteSprite()
    {
        if (sWhiteSprite != null) return;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true); // make it non-readable afterwards
        sWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        sWhiteSprite.name = "StationUI_WhiteSprite";
        sWhiteSprite.hideFlags = HideFlags.DontSave;
    }

    private void EnsureUI()
    {
        if (worldCanvas == null)
        {
            var go = new GameObject("ProgressCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer));
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            cv.sortingOrder = 1000;
            worldCanvas = cv;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one;
        }

        if (root == null)
        {
            var rootGO = new GameObject("Root", typeof(RectTransform));
            root = rootGO.GetComponent<RectTransform>();
            root.SetParent(worldCanvas.transform, false);
            root.sizeDelta = size;
        }

        // Background panel (solid color)
        var bg = root.Find("BG") as RectTransform;
        if (bg == null)
        {
            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgImg = bgGO.GetComponent<Image>();
            bg = bgGO.GetComponent<RectTransform>();
            bg.SetParent(root, false);
            bg.anchorMin = new Vector2(0, 0);
            bg.anchorMax = new Vector2(1, 1);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            bgImg.sprite = sWhiteSprite;
            bgImg.color = bgColor;
            bgImg.type = Image.Type.Simple;
        }
        else
        {
            var bgImg = bg.GetComponent<Image>();
            if (bgImg != null && bgImg.sprite == null) bgImg.sprite = sWhiteSprite;
        }

        // Bar background rect
        barBgRect = root.Find("BarBG") as RectTransform;
        if (barBgRect == null)
        {
            var barBgGO = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
            var barBgImg = barBgGO.GetComponent<Image>();
            barBgRect = barBgGO.GetComponent<RectTransform>();
            barBgRect.SetParent(root, false);
            barBgRect.anchorMin = new Vector2(0.02f, 0.2f);
            barBgRect.anchorMax = new Vector2(0.98f, 0.8f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            barBgImg.sprite = sWhiteSprite;
            barBgImg.color = new Color(1f, 1f, 1f, 0.15f);
            barBgImg.type = Image.Type.Simple;
        }
        else
        {
            var barBgImg = barBgRect.GetComponent<Image>();
            if (barBgImg != null && barBgImg.sprite == null) barBgImg.sprite = sWhiteSprite;
        }

        // Bar fill inside background, driven by anchors
        barFillRect = (barBgRect != null) ? barBgRect.Find("BarFill") as RectTransform : null;
        if (barFillRect == null)
        {
            var barFillGO = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillImg = barFillGO.GetComponent<Image>();
            barFillRect = barFillGO.GetComponent<RectTransform>();
            barFillRect.SetParent(barBgRect, false);
        }
        else
        {
            barFillImg = barFillRect.GetComponent<Image>();
        }
        if (barFillImg != null)
        {
            barFillImg.sprite = sWhiteSprite;
            barFillImg.color = fillColor;
            barFillImg.type = Image.Type.Simple;
        }
        // Ensure left anchor fixed at 0 and start empty — NO padding (world units would hide the bar)
        barFillRect.anchorMin = new Vector2(0, 0);
        barFillRect.anchorMax = new Vector2(0, 1);
        barFillRect.pivot = new Vector2(0, 0.5f);
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;
        SetProgress(0f);

        // Label small single-line
        var labelTf = root.Find("Label") as RectTransform;
        if (labelTf == null)
        {
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelTf = labelGO.GetComponent<RectTransform>();
            labelTf.SetParent(root, false);
            labelTf.anchorMin = new Vector2(0, 0);
            labelTf.anchorMax = new Vector2(1, 1);
            labelTf.offsetMin = new Vector2(6, 0);
            labelTf.offsetMax = new Vector2(-6, 0);

            tmpLabel = labelGO.AddComponent<TextMeshProUGUI>();
            if (tmpLabel != null)
            {
                tmpLabel.enableAutoSizing = false;
                tmpLabel.fontSize = 8;
                tmpLabel.enableWordWrapping = false;
                tmpLabel.overflowMode = TextOverflowModes.Ellipsis;
                tmpLabel.alignment = TextAlignmentOptions.Midline;
                tmpLabel.color = Color.white;
                tmpLabel.text = "";
            }
            else
            {
                uiLabel = labelGO.AddComponent<Text>();
                uiLabel.resizeTextForBestFit = false;
                uiLabel.fontSize = 10;
                uiLabel.alignment = TextAnchor.MiddleCenter;
                uiLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                uiLabel.verticalOverflow = VerticalWrapMode.Truncate;
                uiLabel.color = Color.white;
                uiLabel.text = "";
            }
        }
    }
}
