using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// Simple world-space bubble above a customer showing their order
public class CustomerOrderUI : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private bool positionAboveAnchor = true;
    [SerializeField, Min(0f)] private float heightAbove = 0.4f; // lower than before
    [SerializeField, Min(0f)] private float lateralOffset = 0.25f; // more to the left

    [Header("UI")] 
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform root;
    [SerializeField] private Image bg; // optional background (kept off by default)
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text uiText;

    [Header("Style")]
    [SerializeField, Min(0.001f)] private float canvasScale = 0.01f; // requested 0.01
    [SerializeField] private Vector2 bubbleSize = new Vector2(1.5f, 0.5f); // used if autoSizeToText=false
    [SerializeField] private bool autoSizeToText = true;
    [SerializeField] private Vector2 padding = new Vector2(0.25f, 0.12f); // x=left/right, y=top/bottom (canvas units)
    [SerializeField] private float iconToTextGap = 0.1f;
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private Color bgColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private bool showBackground = false;

    [Header("Border Bubble")]
    [SerializeField] private bool showBorder = true;
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField, Range(0.05f, 0.9f)] private float cornerRadiusFrac = 0.6f; // fraction of height for rounded corners
    [SerializeField, Range(0.02f, 0.4f)] private float borderThicknessFrac = 0.14f; // fraction of height for border thickness
    [SerializeField] private bool showTail = true;
    [SerializeField, Range(0.05f, 0.6f)] private float tailSize = 0.25f; // relative to height
    [SerializeField, Range(-0.6f, 0.6f)] private float tailOffsetX = 0.1f; // -left to +right, relative to width

    // Rounded border image (single Image using a generated 9-sliced sprite)
    private Image roundBorderImg;

    // Legacy straight lines (kept but disabled when using rounded)
    private RectTransform borderTop, borderBottom, borderLeft, borderRight, tail;

    [Header("Pulse (auto show/hide)")]
    [SerializeField] private bool autoPulse = true;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float showDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float interval = 3f;

    private Camera cam;
    private CanvasGroup canvasGroup;
    private Coroutine pulseRoutine;

    // runtime 1x1 white sprite to ensure UI Images render (borders, tail, bg)
    private static Sprite sWhiteSprite;
    // cached rounded border sprite
    private static Sprite sRoundedBorderSprite;

    // Add a small patience/progress bar at the bottom of bubble
    private RectTransform progressBg;
    private RectTransform progressFill;
    private Image progressFillImg;
    [SerializeField] private Color progressBgColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Color progressFillColor = new Color(1f, 0.3f, 0.2f, 0.95f);

    private void Awake()
    {
        cam = Camera.main;
        if (anchor == null) anchor = transform;
        EnsureWhiteSprite();
        EnsureUI();
        EnsureRoundedBorder();
        // keep legacy borders disabled
        EnsureLegacyBorders();
        ApplyStyle();
        // Always start hidden; will be shown explicitly by Customer when arriving at queue spot.
        Hide();
    }

    private void LateUpdate()
    {
        if (worldCanvas == null) return;
        Vector3 basePos = anchor.position;
        if (positionAboveAnchor)
        {
            var col = anchor.GetComponentInParent<Collider>();
            float topY = basePos.y;
            if (col != null) topY = col.bounds.max.y;
            basePos = new Vector3(basePos.x, topY + heightAbove, basePos.z);
        }
        else
        {
            basePos += worldOffset;
        }

        if (cam == null) cam = Camera.main;
        // Apply lateral offset to the left in screen space (relative to camera right)
        if (cam != null && lateralOffset > 0f)
        {
            basePos += -cam.transform.right * lateralOffset;
        }

        worldCanvas.transform.position = basePos;

        // Billboard: face camera but keep upright (no tilt)
        if (cam != null)
        {
            Vector3 toCam = worldCanvas.transform.position - cam.transform.position; // canvas -> camera
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) toCam = cam.transform.forward;
            worldCanvas.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        // Keep bubble tight to text when autoSize is enabled
        if (autoSizeToText)
        {
            ResizeToContent();
            UpdateBorderLayout();
        }
    }

    public void Set(Sprite itemIcon, string label)
    {
        if (icon != null)
        {
            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null; // hide icon if none
        }
        if (tmpText != null) { tmpText.text = label; tmpText.color = labelColor; }
        if (uiText != null) { uiText.text = label; uiText.color = labelColor; }
        UpdateLabelPadding();
        if (tmpText != null) tmpText.transform.SetAsLastSibling();
        if (uiText != null) uiText.transform.SetAsLastSibling();
        if (autoSizeToText)
        {
            ResizeToContent();
            UpdateBorderLayout();
        }
        // do not auto-show here; Customer controls when to show
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        EnsureProgressBar();
        if (progressFill != null)
        {
            var aMin = progressFill.anchorMin;
            var aMax = progressFill.anchorMax;
            aMin.x = 0f;
            aMax.x = Mathf.Max(0.001f, t);
            progressFill.anchorMin = aMin;
            progressFill.anchorMax = aMax;
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }
    }

    private void ResizeToContent()
    {
        if (root == null) return;
        float iconW = 0f, iconH = 0f;
        if (icon != null && icon.enabled)
        {
            iconH = Mathf.Max(root.sizeDelta.y, 0.3f);
            iconW = iconH; // square icon
        }

        float txtW = 0.6f, txtH = 0.35f; // sensible defaults
        if (tmpText != null)
        {
            Vector2 pref = tmpText.GetPreferredValues(tmpText.text);
            txtW = Mathf.Max(pref.x, 0.2f);
            txtH = Mathf.Max(pref.y, 0.25f);
        }
        else if (uiText != null)
        {
            var gen = uiText.cachedTextGeneratorForLayout;
            var settings = uiText.GetGenerationSettings(new Vector2(10000, 0));
            float w = gen.GetPreferredWidth(uiText.text, settings) / uiText.pixelsPerUnit;
            float h = gen.GetPreferredHeight(uiText.text, settings) / uiText.pixelsPerUnit;
            txtW = Mathf.Max(w, 0.2f);
            txtH = Mathf.Max(h, 0.25f);
        }

        float contentH = Mathf.Max(txtH, iconH);
        float totalW = padding.x + iconW + (iconW > 0 ? iconToTextGap : 0) + txtW + padding.x;
        float totalH = padding.y + contentH + padding.y;
        root.sizeDelta = new Vector2(totalW, totalH);

        // layout icon
        if (icon != null)
        {
            var rt = icon.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(iconW, 0);
            rt.anchoredPosition = new Vector2(0, 0);
        }

        // layout label
        float leftPad = padding.x + (iconW > 0 ? iconW + iconToTextGap : 0);
        if (tmpText != null)
        {
            var rt = (RectTransform)tmpText.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(leftPad, padding.y);
            rt.offsetMax = new Vector2(-padding.x, -padding.y);
        }
        if (uiText != null)
        {
            var rt = (RectTransform)uiText.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(leftPad, padding.y);
            rt.offsetMax = new Vector2(-padding.x, -padding.y);
        }
    }

    private void UpdateLabelPadding()
    {
        // no-op when auto-size handles layout
        if (autoSizeToText) return;
        float leftPad = padding.x;
        if (icon != null && icon.enabled)
        {
            leftPad += bubbleSize.y * 0.9f + iconToTextGap;
        }
        if (tmpText != null)
        {
            var rt = (RectTransform)tmpText.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(leftPad, padding.y);
            rt.offsetMax = new Vector2(-padding.x, -padding.y);
        }
        if (uiText != null)
        {
            var rt = (RectTransform)uiText.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(leftPad, padding.y);
            rt.offsetMax = new Vector2(-padding.x, -padding.y);
        }
    }

    public void Show()
    {
        if (worldCanvas != null) worldCanvas.gameObject.SetActive(true);
        if (autoPulse)
        {
            // Start pulsing visibility when shown if enabled
            StartPulse();
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void Hide()
    {
        // Ensure pulse stops while hidden so it can't fade in while walking
        StopPulse();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (worldCanvas != null) worldCanvas.gameObject.SetActive(true);
    }

    public void StartPulse()
    {
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        // Ensure visible before starting pulse loop
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        pulseRoutine = StartCoroutine(PulseLoop());
    }

    public void StopPulse()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private IEnumerator PulseLoop()
    {
        while (true)
        {
            yield return FadeTo(1f, fadeDuration);
            yield return new WaitForSeconds(showDuration);
            yield return FadeTo(0f, fadeDuration);
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (canvasGroup == null || duration <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = target;
            yield break;
        }
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    private void EnsureWhiteSprite()
    {
        if (sWhiteSprite != null) return;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        sWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        sWhiteSprite.name = "CustomerUI_WhiteSprite";
        sWhiteSprite.hideFlags = HideFlags.DontSave;
    }

    private void EnsureUI()
    {
        if (worldCanvas == null)
        {
            var cvGO = new GameObject("OrderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer));
            var cv = cvGO.GetComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            cv.sortingOrder = 1001;
            worldCanvas = cv;
            cvGO.transform.SetParent(transform, false);
        }
        if (canvasGroup == null)
        {
            canvasGroup = worldCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = worldCanvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f; // start hidden
        }
        if (root == null)
        {
            var rootGO = new GameObject("Root", typeof(RectTransform));
            root = rootGO.GetComponent<RectTransform>();
            root.SetParent(worldCanvas.transform, false);
        }
        if (bg == null)
        {
            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bg = bgGO.GetComponent<Image>();
            var rt = bgGO.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            bg.raycastTarget = false;
            bg.sprite = sWhiteSprite; // ensure visible when enabled
            bg.type = Image.Type.Simple;
        }
        if (icon == null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon = iconGO.GetComponent<Image>();
            var rt = iconGO.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        if (tmpText == null && uiText == null)
        {
            var labelGO = new GameObject("Label", typeof(RectTransform));
            var rt = labelGO.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(0.45f, 0);
            rt.offsetMax = new Vector2(0, 0);

            tmpText = labelGO.AddComponent<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.enableAutoSizing = false;
                tmpText.fontSize = 16f;
                tmpText.enableWordWrapping = false;
                tmpText.overflowMode = TextOverflowModes.Overflow;
                tmpText.alignment = TextAlignmentOptions.MidlineLeft;
                tmpText.color = labelColor;
                tmpText.raycastTarget = false;
            }
            else
            {
                uiText = labelGO.AddComponent<Text>();
                uiText.resizeTextForBestFit = false;
                uiText.fontSize = 18;
                uiText.alignment = TextAnchor.MiddleLeft;
                uiText.color = labelColor;
                uiText.raycastTarget = false;
            }
        }
    }

    private void EnsureRoundedBorder()
    {
        if (roundBorderImg == null)
        {
            var go = new GameObject("RoundedBorder", typeof(RectTransform), typeof(Image));
            roundBorderImg = go.GetComponent<Image>();
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            roundBorderImg.raycastTarget = false;
            roundBorderImg.type = Image.Type.Sliced;
        }

        if (sRoundedBorderSprite == null)
        {
            sRoundedBorderSprite = GenerateRoundedBorderSprite(128, 64, 0.45f, 0.15f);
        }
        roundBorderImg.sprite = sRoundedBorderSprite;
    }

    private void EnsureLegacyBorders()
    {
        // Keep legacy lines/tail inactive when using rounded border
        Transform borderRoot = root != null ? root.Find("Border") : null;
        if (borderRoot != null) borderRoot.gameObject.SetActive(false);
        // But keep tail if needed (we create a separate under RoundedTail)
        if (tail == null)
        {
            var go = new GameObject("RoundedTail", typeof(RectTransform), typeof(Image));
            tail = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = borderColor;
            img.sprite = sWhiteSprite;
            img.type = Image.Type.Simple;
            tail.SetParent(root, false);
        }
    }

    private static Sprite GenerateRoundedBorderSprite(int texW, int texH, float radiusFrac, float thicknessFrac)
    {
        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        int r = Mathf.RoundToInt(Mathf.Min(texW, texH) * radiusFrac);
        r = Mathf.Clamp(r, 2, Mathf.Min(texW, texH) / 2 - 1);
        int t = Mathf.RoundToInt(texH * thicknessFrac);
        t = Mathf.Clamp(t, 2, texH / 2 - 1);

        // Helper local to test inside rounded rect
        bool InsideRR(int x, int y, int w, int h, int rad)
        {
            int left = rad;
            int right = w - 1 - rad;
            int bottom = rad;
            int top = h - 1 - rad;
            if (x >= left && x <= right) return true;
            if (y >= bottom && y <= top) return true;
            // corners
            int dx, dy;
            if (x < left && y < bottom) { dx = x - left; dy = y - bottom; return (dx * dx + dy * dy) <= rad * rad; }
            if (x > right && y < bottom) { dx = x - right; dy = y - bottom; return (dx * dx + dy * dy) <= rad * rad; }
            if (x < left && y > top) { dx = x - left; dy = y - top; return (dx * dx + dy * dy) <= rad * rad; }
            if (x > right && y > top) { dx = x - right; dy = y - top; return (dx * dx + dy * dy) <= rad * rad; }
            return false;
        }

        // Draw outer
        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                if (InsideRR(x, y, texW, texH, r)) tex.SetPixel(x, y, white);
            }
        }
        // Cut inner
        int rInner = Mathf.Max(1, r - t);
        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                if (InsideRR(x - t, y - t, texW - 2 * t, texH - 2 * t, rInner)) tex.SetPixel(x, y, clear);
            }
        }

        tex.Apply(false, true);
        // Create 9-sliced sprite; border equals corner radius so corners don't stretch
        var sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        sprite.name = "RoundedBorderSprite";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private void UpdateBorderLayout()
    {
        if (roundBorderImg != null)
        {
            roundBorderImg.enabled = showBorder;
            roundBorderImg.color = borderColor;
            // If needed, regenerate sprite when fractions change significantly (skipped for simplicity)
        }

        // Tail (small diamond), positioned near bottom-left by tailOffsetX
        if (tail != null)
        {
            bool tailActive = showBorder && showTail;
            tail.gameObject.SetActive(tailActive);
            if (tailActive)
            {
                float h = root.sizeDelta.y * tailSize; // diamond size
                tail.sizeDelta = new Vector2(h, h);
                tail.anchorMin = new Vector2(0.5f + tailOffsetX, 0);
                tail.anchorMax = tail.anchorMin;
                tail.pivot = new Vector2(0.5f, 1f);
                tail.anchoredPosition = new Vector2(0, -h * 0.35f); // slightly outside
                tail.localRotation = Quaternion.Euler(0, 0, 45f); // diamond shape
                var img = tail.GetComponent<Image>();
                img.color = borderColor;
            }
        }
    }

    private void ApplyStyle()
    {
        if (worldCanvas != null)
        {
            worldCanvas.transform.localScale = Vector3.one * canvasScale;
        }
        if (root != null)
        {
            if (!autoSizeToText)
            {
                root.sizeDelta = bubbleSize;
            }
        }
        if (bg != null)
        {
            bg.enabled = showBackground;
            if (showBackground)
            {
                bg.color = bgColor;
                bg.sprite = sWhiteSprite;
                bg.type = Image.Type.Simple;
            }
        }
        if (icon != null)
        {
            var rt = icon.rectTransform;
            var h = autoSizeToText && root != null ? root.sizeDelta.y : bubbleSize.y;
            rt.sizeDelta = new Vector2(h * 0.8f, 0f);
        }
        if (tmpText != null) tmpText.color = labelColor;
        if (uiText != null) uiText.color = labelColor;
        if (autoSizeToText)
        {
            ResizeToContent();
        }
        UpdateBorderLayout();
    }

    private void EnsureProgressBar()
    {
        if (root == null) return;
        if (progressBg == null)
        {
            var bgGO = new GameObject("PatienceBG", typeof(RectTransform), typeof(Image));
            progressBg = bgGO.GetComponent<RectTransform>();
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = progressBgColor;
            bgImg.raycastTarget = false;
            progressBg.SetParent(root, false);
            progressBg.anchorMin = new Vector2(0.08f, 0.02f);
            progressBg.anchorMax = new Vector2(0.92f, 0.12f);
            progressBg.offsetMin = Vector2.zero;
            progressBg.offsetMax = Vector2.zero;
        }
        if (progressFill == null)
        {
            var fillGO = new GameObject("PatienceFill", typeof(RectTransform), typeof(Image));
            progressFill = fillGO.GetComponent<RectTransform>();
            progressFillImg = fillGO.GetComponent<Image>();
            progressFillImg.color = progressFillColor;
            progressFillImg.raycastTarget = false;
            progressFill.SetParent(progressBg, false);
            progressFill.anchorMin = new Vector2(0f, 0f);
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }
    }
}
