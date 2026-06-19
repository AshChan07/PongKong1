#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds a functional PowerUpHUD in the currently open scene and wires every
// reference (manager, score, ledges, slot icons, texts, event channels).
// Run via Tools ▸ Build PowerUp HUD. Re-running replaces the existing HUD.
public static class PowerUpHUDBuilder
{
    private const string EventsPath = "Assets/_Game/Events/";
    private static readonly int[] Costs = { 1, 1, 2, 4 };

    [MenuItem("Tools/Build PowerUp HUD")]
    public static void Build()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Build PowerUp HUD",
                "No Canvas found in the open scene. Open the Game Scene first.", "OK");
            return;
        }

        var manager = Object.FindFirstObjectByType<PowerUpManager>();
        var score = Object.FindFirstObjectByType<ScoreManager>();
        LedgeController p1Ledge = null, p2Ledge = null;
        foreach (var l in Object.FindObjectsByType<LedgeController>(FindObjectsSortMode.None))
        {
            if (l.Side == PlayerSide.P1) p1Ledge = l;
            else p2Ledge = l;
        }

        // Remove a previous HUD so the menu item is idempotent.
        var existing = Object.FindFirstObjectByType<PowerUpHUD>();
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject root = NewUI("PowerUp HUD", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        var hud = root.AddComponent<PowerUpHUD>();

        Image[] p1Slots = BuildSlots(root.transform, "P1", true);
        Image[] p2Slots = BuildSlots(root.transform, "P2", false);
        TextMeshProUGUI p1Sp = BuildText(root.transform, "P1 Spendable", true, -116f, TextAlignmentOptions.TopLeft);
        TextMeshProUGUI p2Sp = BuildText(root.transform, "P2 Spendable", false, -116f, TextAlignmentOptions.TopRight);
        TextMeshProUGUI p1Lives = BuildText(root.transform, "P1 Lives", true, -146f, TextAlignmentOptions.TopLeft);
        TextMeshProUGUI p2Lives = BuildText(root.transform, "P2 Lives", false, -146f, TextAlignmentOptions.TopRight);

        var so = new SerializedObject(hud);
        SetRef(so, "powerUpManager", manager);
        SetRef(so, "scoreManager", score);
        SetRef(so, "p1Ledge", p1Ledge);
        SetRef(so, "p2Ledge", p2Ledge);
        SetRef(so, "p1SpendableText", p1Sp);
        SetRef(so, "p2SpendableText", p2Sp);
        SetRef(so, "p1LivesText", p1Lives);
        SetRef(so, "p2LivesText", p2Lives);
        SetArray(so, "p1SlotIcons", p1Slots);
        SetArray(so, "p2SlotIcons", p2Slots);
        SetRef(so, "onPowerUpPrimed", LoadEvent("OnPowerUpPrimed"));
        SetRef(so, "onPowerUpActivated", LoadEvent("OnPowerUpActivated"));
        SetRef(so, "onPowerUpDeactivated", LoadEvent("OnPowerUpDeactivated"));
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("PowerUp HUD built and wired. " +
                  (manager == null ? "WARNING: no PowerUpManager found. " : "") +
                  (score == null ? "WARNING: no ScoreManager found. " : ""));
    }

    private static Image[] BuildSlots(Transform parent, string label, bool left)
    {
        const float size = 48f, gap = 8f, margin = 20f, top = -60f;
        var slots = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject slot = NewUI($"{label} Slot {i + 1}", parent);
            var rt = slot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(left ? 0f : 1f, 1f);
            float x = margin + i * (size + gap);
            rt.anchoredPosition = new Vector2(left ? x : -x, top);

            var img = slot.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            slots[i] = img;

            // Slot label: quick-slot key number over its point cost.
            TextMeshProUGUI lbl = BuildLabel(slot.transform);
            lbl.text = $"{i + 1}\n<size=10>{Costs[i]}pt</size>";
        }
        return slots;
    }

    private static TextMeshProUGUI BuildLabel(Transform slot)
    {
        GameObject go = NewUI("Label", slot);
        Stretch(go.GetComponent<RectTransform>());
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TextMeshProUGUI BuildText(Transform parent, string name, bool left, float y, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260f, 28f);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(left ? 0f : 1f, 1f);
        rt.anchoredPosition = new Vector2(left ? 20f : -20f, y);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20f;
        tmp.alignment = align;
        tmp.color = new Color(0f, 1f, 0f, 1f);
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject so, string field, Object[] values)
    {
        var p = so.FindProperty(field);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static PowerUpDataGameEvent LoadEvent(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<PowerUpDataGameEvent>(EventsPath + assetName + ".asset");
    }
}
#endif
