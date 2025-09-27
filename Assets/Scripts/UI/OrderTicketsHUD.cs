using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OrderTicketsHUD : MonoBehaviour
{
    public static OrderTicketsHUD Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private RectTransform container; // parent for items
    [SerializeField] private Vector2 itemSize = new Vector2(300, 44);
    [SerializeField] private float itemSpacing = 6f;

    [Header("Style")]
    [SerializeField] private Color bgColor = new Color(0,0,0,0.5f);
    [SerializeField] private Color barBgColor = new Color(0,0,0,0.0f); // Invisible background to remove big white rectangles
    [SerializeField] private Color barFillColor = new Color(0.2f,0.8f,0.2f,0.9f);
    [SerializeField] private Color textColor = Color.white;

    private readonly List<Group> groups = new List<Group>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureCanvasAndContainer();
    }

    private void Update()
    {
        groups.RemoveAll(g => g == null || g.customers == null || g.customers.Count == 0);
        groups.Sort((a,b) => Remaining(a).CompareTo(Remaining(b)));

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null) continue;
            float rem01 = Remaining01(g);
            g.fill.anchorMin = new Vector2(0,0);
            g.fill.anchorMax = new Vector2(Mathf.Clamp01(rem01),1);
            g.fill.offsetMin = Vector2.zero;
            g.fill.offsetMax = Vector2.zero;

            if (g.labelTMP != null) g.labelTMP.text = BuildLabel(g);
            if (g.labelUI != null) g.labelUI.text = BuildLabel(g);

            if (g.iconImg != null)
            {
                g.iconImg.enabled = g.iconImg.sprite != null;
            }

            g.root.anchoredPosition = new Vector2(0, -i * (itemSize.y + itemSpacing));
        }

        if (container != null)
        {
            float h = groups.Count * itemSize.y + Mathf.Max(0, groups.Count - 1) * itemSpacing;
            container.sizeDelta = new Vector2(container.sizeDelta.x, h);
        }
    }

    public void Register(Customer c)
    {
        if (c == null || c.order == null || c.order.requiredItem == null) return;
        var key = new GroupKey(c.order.requiredItem, c.order.quantity);
        var g = groups.Find(gr => gr.key.Equals(key));
        if (g == null)
        {
            g = CreateGroup(key, c.order.requiredItem.icon);
            groups.Add(g);
        }
        if (!g.customers.Contains(c)) g.customers.Add(c);
    }

    public void Unregister(Customer c)
    {
        if (c == null || c.order == null || c.order.requiredItem == null) return;
        var key = new GroupKey(c.order.requiredItem, c.order.quantity);
        int gi = groups.FindIndex(gr => gr.key.Equals(key));
        if (gi >= 0)
        {
            var g = groups[gi];
            g.customers.Remove(c);
            if (g.customers.Count == 0)
            {
                if (g.root != null) Destroy(g.root.gameObject);
                groups.RemoveAt(gi);
            }
        }
    }

    private float Remaining(Group g)
    {
        float best = float.MaxValue;
        for (int i = g.customers.Count - 1; i >= 0; i--)
        {
            var c = g.customers[i];
            if (c == null)
            {
                g.customers.RemoveAt(i);
                continue;
            }
            float rem = Mathf.Max(0, c.WaitMax - c.WaitElapsed);
            if (rem < best) best = rem;
        }
        return best == float.MaxValue ? 0f : best;
    }

    private float Remaining01(Group g)
    {
        float best = float.MaxValue;
        foreach (var c in g.customers)
        {
            if (c == null || c.WaitMax <= 0.0001f) continue;
            float rem = Mathf.Clamp01((c.WaitMax - c.WaitElapsed) / c.WaitMax);
            if (rem < best) best = rem;
        }
        return best == float.MaxValue ? 0f : best;
    }

    private string BuildLabel(Group g)
    {
        Customer sample = null;
        foreach (var c in g.customers) { if (c != null) { sample = c; break; } }
        string name = sample != null && sample.order != null && sample.order.requiredItem != null ? sample.order.requiredItem.displayName : "(Unknown)";
        int qty = sample != null && sample.order != null ? sample.order.quantity : 1;
        float rem = Remaining(g);
        TimeSpan ts = TimeSpan.FromSeconds(rem);
        string t = ts.Minutes > 0 ? $"{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Seconds:D2}s";
        // Removed count from label per request
        return $"{name} x{qty}  —  {t}";
    }

    private void EnsureCanvasAndContainer()
    {
        if (container != null) return;
        var canvasGO = new GameObject("OrderTicketsHUD", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var rootGO = new GameObject("Container", typeof(RectTransform));
        container = rootGO.GetComponent<RectTransform>();
        container.SetParent(canvasGO.transform, false);
        container.anchorMin = new Vector2(1, 1);
        container.anchorMax = new Vector2(1, 1);
        container.pivot = new Vector2(1, 1);
        container.anchoredPosition = new Vector2(-20, -20);
        container.sizeDelta = new Vector2(320, 0);
    }

    private Group CreateGroup(GroupKey key, Sprite icon)
    {
        EnsureCanvasAndContainer();

        var itemGO = new GameObject("TicketGroup", typeof(RectTransform));
        var itemRT = itemGO.GetComponent<RectTransform>();
        itemRT.SetParent(container, false);
        itemRT.sizeDelta = itemSize;

        var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
        var bgRT = bgGO.GetComponent<RectTransform>();
        var bgImg = bgGO.GetComponent<Image>();
        bgRT.SetParent(itemRT, false);
        bgRT.anchorMin = new Vector2(0, 0);
        bgRT.anchorMax = new Vector2(1, 1);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgImg.color = bgColor;

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRT = iconGO.GetComponent<RectTransform>();
        var iconImg = iconGO.GetComponent<Image>();
        iconRT.SetParent(itemRT, false);
        iconRT.anchorMin = new Vector2(0f, 0f);
        iconRT.anchorMax = new Vector2(0f, 1f);
        iconRT.sizeDelta = new Vector2(itemSize.y - 8f, 0f);
        iconRT.anchoredPosition = new Vector2(6f, 0f);
        iconImg.sprite = icon;
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;

        var barBGGO = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
        var barBGRT = barBGGO.GetComponent<RectTransform>();
        var barBGImg = barBGGO.GetComponent<Image>();
        barBGRT.SetParent(itemRT, false);
        barBGRT.anchorMin = new Vector2(0.22f, 0.24f);
        barBGRT.anchorMax = new Vector2(0.98f, 0.30f);
        barBGRT.offsetMin = Vector2.zero;
        barBGRT.offsetMax = Vector2.zero;
        barBGImg.color = barBgColor;
        barBGImg.enabled = barBgColor.a > 0.01f; // invisible by default

        var barFillGO = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        var barFillRT = barFillGO.GetComponent<RectTransform>();
        var barFillImg = barFillGO.GetComponent<Image>();
        barFillRT.SetParent(barBGRT, false);
        barFillRT.anchorMin = new Vector2(0f, 0f);
        barFillRT.anchorMax = new Vector2(0f, 1f);
        barFillRT.offsetMin = Vector2.zero;
        barFillRT.offsetMax = Vector2.zero;
        barFillImg.color = barFillColor;

        TMP_Text labelTMP = null;
        Text labelUI = null;
        var labelGO = new GameObject("Label", typeof(RectTransform));
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.SetParent(itemRT, false);
        labelRT.anchorMin = new Vector2(0.22f, 0.35f);
        labelRT.anchorMax = new Vector2(0.98f, 0.95f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        if (labelTMP != null)
        {
            labelTMP.enableAutoSizing = false;
            labelTMP.fontSize = 16f;
            labelTMP.enableWordWrapping = false;
            labelTMP.overflowMode = TextOverflowModes.Ellipsis;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            labelTMP.color = textColor;
        }
        else
        {
            labelUI = labelGO.AddComponent<Text>();
            labelUI.resizeTextForBestFit = false;
            labelUI.fontSize = 14;
            labelUI.alignment = TextAnchor.UpperLeft;
            labelUI.color = textColor;
        }

        return new Group
        {
            key = key,
            customers = new List<Customer>(),
            root = itemRT,
            fill = barFillRT,
            labelTMP = labelTMP,
            labelUI = labelUI,
            iconImg = iconImg
        };
    }

    private struct GroupKey
    {
        public ItemDefinition item;
        public int qty;
        public GroupKey(ItemDefinition i, int q) { item = i; qty = q; }
        public override bool Equals(object obj)
        {
            if (!(obj is GroupKey)) return false;
            var o = (GroupKey)obj;
            return o.item == item && o.qty == qty;
        }
        public override int GetHashCode()
        {
            return (item != null ? item.GetHashCode() : 0) * 397 ^ qty;
        }
    }

    private class Group
    {
        public GroupKey key;
        public List<Customer> customers;
        public RectTransform root;
        public RectTransform fill;
        public TMP_Text labelTMP;
        public Text labelUI;
        public Image iconImg;
    }
}
