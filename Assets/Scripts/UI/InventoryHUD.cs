using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;

    [Header("UI Containers")]
    [SerializeField] private Transform listRoot; // stable parent container
    [SerializeField] private GameObject entryPrefab; // contains icon + text

    public enum LayoutMode { Vertical, Horizontal, Grid }

    [Header("Layout")] 
    [SerializeField] private LayoutMode layoutMode = LayoutMode.Horizontal; // default to side-by-side
    [SerializeField] private float spacing = 8f;
    [SerializeField] private Vector2 gridCellSize = new Vector2(120, 24);
    [SerializeField] private Vector2 gridSpacing = new Vector2(6, 6);

    [Header("Display")] 
    [Tooltip("Show one row per unique item (sum counts across slots). If off, shows one row per non-empty slot.")]
    [SerializeField] private bool groupSameItems = true;
    [SerializeField] private int tmpLabelFontSize = 14; // smaller default
    [SerializeField] private int uiLabelFontSize = 12;  // smaller default

    // Internal content root we fully control (to avoid conflicts with user layout components)
    private Transform contentRoot;

    private void Start()
    {
        TryAutoWire();
        EnsureListRoot();
        EnsureContentRoot();
        Subscribe();
        Rebuild();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.onChanged.RemoveListener(Rebuild);
        }
    }

    private void TryAutoWire()
    {
        if (inventory == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                inventory = player.GetComponentInChildren<Inventory>();
                if (inventory == null) inventory = player.GetComponent<Inventory>();
            }
        }
    }

    private void Subscribe()
    {
        if (inventory == null) TryAutoWire();
        if (inventory != null)
        {
            inventory.onChanged.RemoveListener(Rebuild); // avoid duplicates
            inventory.onChanged.AddListener(Rebuild);
        }
    }

    private void EnsureListRoot()
    {
        if (listRoot != null) return;

        // Auto-create a container anchored top-left (no LayoutGroup on this object to avoid conflicts)
        var go = new GameObject("InventoryList_Auto", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(16f, -16f);
        rt.sizeDelta = new Vector2(320f, 0f);

        // Optional: auto-size vertically
        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        listRoot = go.transform;
    }

    private void EnsureContentRoot()
    {
        // Recreate or create content root if missing
        if (contentRoot == null || contentRoot.parent != listRoot)
        {
            contentRoot = new GameObject("Content", typeof(RectTransform)).transform;
            var rt = (RectTransform)contentRoot;
            rt.SetParent(listRoot, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
        }

        // Ensure only the desired layout exists on contentRoot
        var v = contentRoot.GetComponent<VerticalLayoutGroup>();
        var h = contentRoot.GetComponent<HorizontalLayoutGroup>();
        var g = contentRoot.GetComponent<GridLayoutGroup>();

        if (layoutMode == LayoutMode.Horizontal)
        {
            if (v != null) Destroy(v);
            if (g != null) Destroy(g);
            if (h == null) h = contentRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childControlWidth = false;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            h.spacing = spacing;
        }
        else if (layoutMode == LayoutMode.Vertical)
        {
            if (h != null) Destroy(h);
            if (g != null) Destroy(g);
            if (v == null) v = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = false;
            v.spacing = spacing;
        }
        else // Grid
        {
            if (h != null) Destroy(h);
            if (v != null) Destroy(v);
            if (g == null) g = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = gridCellSize;
            g.spacing = gridSpacing;
            g.startCorner = GridLayoutGroup.Corner.UpperLeft;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            g.childAlignment = TextAnchor.UpperLeft;
        }
    }

    public void Rebuild()
    {
        EnsureListRoot();
        EnsureContentRoot();
        if (inventory == null || contentRoot == null) return;

        // Clear existing entries
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        if (groupSameItems)
        {
            // Sum counts per unique ItemDefinition
            var totals = new Dictionary<ItemDefinition, int>();
            foreach (var slot in inventory.Slots)
            {
                if (slot.item == null || slot.count <= 0) continue;
                if (!totals.ContainsKey(slot.item)) totals[slot.item] = 0;
                totals[slot.item] += slot.count;
            }

            foreach (var kvp in totals)
            {
                var def = kvp.Key;
                int count = kvp.Value;
                var go = CreateEntry();
                var label = $"{def.displayName} x{count}";
                ApplyToEntry(go, def.icon, label);
            }
        }
        else
        {
            // One row per slot
            foreach (var slot in inventory.Slots)
            {
                if (slot.item == null || slot.count <= 0) continue;
                var go = CreateEntry();
                var label = $"{slot.item.displayName} x{slot.count}";
                ApplyToEntry(go, slot.item.icon, label);
            }
        }
    }

    private void ApplyToEntry(GameObject go, Sprite iconSprite, string label)
    {
        var elem = go.GetComponent<InventoryHUDElement>();
        if (elem != null)
        {
            elem.Set(iconSprite, label);
        }
        else
        {
            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = false;
                tmp.fontSize = tmpLabelFontSize;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.text = label;
            }
            var ugui = go.GetComponentInChildren<Text>();
            if (ugui != null)
            {
                ugui.fontSize = uiLabelFontSize;
                ugui.alignment = TextAnchor.MiddleLeft;
                ugui.text = label;
            }
        }

        var icon = go.GetComponentInChildren<Image>();
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
        }
    }

    private GameObject CreateEntry()
    {
        if (entryPrefab != null)
        {
            return Instantiate(entryPrefab, contentRoot);
        }

        // Fallback: create a simple row with optional icon and TMP/Text label
        var row = new GameObject("Entry", typeof(RectTransform));
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.SetParent(contentRoot, false);

        // Layout container for content INSIDE the entry (horizontal)
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.spacing = 6f;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;

        // Icon
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.SetParent(rowRt, false);
        iconRt.sizeDelta = new Vector2(16, 16);
        var icon = iconGO.GetComponent<Image>();
        icon.preserveAspect = true;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.SetParent(rowRt, false);

        // Prefer TMP if available
        var tmp = labelGO.AddComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.enableAutoSizing = false;
            tmp.fontSize = tmpLabelFontSize;
            tmp.alignment = TextAlignmentOptions.Left;
        }
        else
        {
            var ugui = labelGO.AddComponent<Text>();
            ugui.fontSize = uiLabelFontSize;
            ugui.alignment = TextAnchor.MiddleLeft;
        }

        // Helper for setting fields uniformly
        row.AddComponent<InventoryHUDElement>();

        return row;
    }
}
