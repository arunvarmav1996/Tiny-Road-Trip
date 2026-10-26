using TinyRoadTrip.Gameplay;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-click, idempotent setup for the distance score and the Game Over summary.
// Builds the summary layout only once (if "Summary" is missing); always (re)wires references.
public static class GameOverSummarySetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string FontPath = "Assets/Fonts Assets/LuckiestGuy-Regular SDF.asset";
    private const float PanelScale = 1.3f; // uniform, so the panel art is not stretched

    private static readonly Color LabelColor = new Color(0.196f, 0.196f, 0.196f);   // matches the RESTART label
    private static readonly Color ValueColor = new Color(0.642f, 0.316f, 0.088f);   // matches the GAME OVER title
    private static readonly Color BadgeColor = new Color(0.95f, 0.35f, 0.12f);

    [MenuItem("Tools/Tiny Road Trip/Set Up Game Over Summary")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError($"[GameOverSummarySetup] Open {ScenePath} first (active: {scene.path}).");
            return;
        }

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        GameObject systems = GameObject.Find("GameplaySystems");
        GameObject scoreUI = GameObject.Find("ScoreUI");
        if (gameManager == null || gameManager.GameOverUI == null || systems == null || scoreUI == null)
        {
            Debug.LogError("[GameOverSummarySetup] Needs GameManager (with GameOverUI), GameplaySystems and ScoreUI. Run Set Up Collectibles first.");
            return;
        }

        // 1. Distance score replaces the Visual Scripting point score on the HUD.
        RunStats runStats = systems.GetComponent<RunStats>() ?? systems.AddComponent<RunStats>();
        Set(runStats, "gameManager", gameManager);
        Set(runStats, "speedController", systems.GetComponent<GameplaySpeedController>());
        Set(runStats, "comboManager", systems.GetComponent<ComboManager>());
        Set(runStats, "coinManager", systems.GetComponent<CoinManager>());
        Set(runStats, "distanceText", scoreUI.GetComponent<TMP_Text>());

        ScriptMachine scoreGraph = scoreUI.GetComponent<ScriptMachine>();
        if (scoreGraph != null) scoreGraph.enabled = false; // kept (disabled) so it can be switched back

        // Bonuses are distance now: near misses go through RunStats, Nitro's extra speed already adds distance.
        ComboManager combo = systems.GetComponent<ComboManager>();
        if (combo != null) Set(combo, "scoreBridge", null);
        NitroEffect nitro = systems.GetComponent<NitroEffect>();
        if (nitro != null)
        {
            Set(nitro, "scoreBridge", null);
            var so = new SerializedObject(nitro);
            so.FindProperty("bonusScorePerSecond").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 2. Game Over panel layout.
        GameObject gameOverUI = gameManager.GameOverUI;
        Transform root = gameOverUI.transform;
        if (root.Find("Summary") == null) BuildLayout(root);

        GameOverSummary summary = gameOverUI.GetComponent<GameOverSummary>() ?? gameOverUI.AddComponent<GameOverSummary>();
        Set(summary, "runStats", runStats);
        Set(summary, "distanceValue", Find<TMP_Text>(root, "Summary/Distance/Value"));
        Set(summary, "bestValue", Find<TMP_Text>(root, "Summary/Best/Value"));
        Set(summary, "coinsValue", Find<TMP_Text>(root, "Summary/Coins/Value"));
        Set(summary, "comboValue", Find<TMP_Text>(root, "Summary/BestCombo/Value"));
        Set(summary, "newBestBadge", root.Find("NewBestBadge"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameOverSummarySetup] Done: distance score, best distance and Game Over summary set up.");
    }

    private static void BuildLayout(Transform root)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // Panel: scale uniformly so the summary fits without distorting the sprite.
        var panel = root.Find("Panel") as RectTransform;
        if (panel != null) panel.sizeDelta *= PanelScale;
        float halfHeight = panel != null ? panel.sizeDelta.y * 0.5f : 400f;

        // Existing elements keep their art, fonts and colours; only their positions change.
        var title = root.Find("GameOverText") as RectTransform;
        if (title != null)
        {
            title.anchoredPosition = new Vector2(0f, halfHeight - 105f);
            title.sizeDelta = new Vector2(800f, 120f);
            var t = title.GetComponent<TMP_Text>();
            if (t != null)
            {
                t.alignment = TextAlignmentOptions.Center;
                t.margin = Vector4.zero; // stray margins pushed the title off its rect
            }
        }

        var restart = root.Find("RestartButton") as RectTransform;
        if (restart != null) restart.anchoredPosition = new Vector2(0f, -halfHeight + 150f);

        var close = root.Find("Close Button") as RectTransform;
        if (close != null) close.anchoredPosition *= PanelScale; // stays at the panel's bottom-right corner

        // NEW BEST badge (hidden until a run beats the best distance).
        var badge = Text("NewBestBadge", root, font, 64, TextAlignmentOptions.Center, BadgeColor,
            new Vector2(0f, halfHeight - 220f), new Vector2(520f, 80f), "NEW BEST!");
        badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
        badge.gameObject.SetActive(false);

        // Stat rows: label left, value right.
        var summary = Rect("Summary", root, new Vector2(0f, 10f), new Vector2(700f, 300f));
        Row(summary, font, "Distance", "DISTANCE", 105f);
        Row(summary, font, "Best", "BEST", 35f);
        Row(summary, font, "Coins", "COINS", -35f);
        Row(summary, font, "BestCombo", "BEST COMBO", -105f);

        // Draw order: panel at the back, then summary and badge, buttons on top.
        int panelIndex = panel != null ? panel.GetSiblingIndex() : 0;
        summary.SetSiblingIndex(panelIndex + 1);
        badge.transform.SetSiblingIndex(panelIndex + 2);
    }

    private static void Row(RectTransform parent, TMP_FontAsset font, string name, string label, float y)
    {
        var row = Rect(name, parent, new Vector2(0f, y), new Vector2(640f, 70f));
        var l = Text("Label", row, font, 50, TextAlignmentOptions.MidlineLeft, LabelColor, Vector2.zero, new Vector2(640f, 70f), label);
        l.rectTransform.anchorMin = new Vector2(0f, 0f);
        l.rectTransform.anchorMax = new Vector2(1f, 1f);
        l.rectTransform.sizeDelta = Vector2.zero;
        var v = Text("Value", row, font, 58, TextAlignmentOptions.MidlineRight, ValueColor, Vector2.zero, new Vector2(640f, 70f), "0");
        v.rectTransform.anchorMin = new Vector2(0f, 0f);
        v.rectTransform.anchorMax = new Vector2(1f, 1f);
        v.rectTransform.sizeDelta = Vector2.zero;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align,
        Color color, Vector2 pos, Vector2 rectSize, string text)
    {
        var rt = Rect(name, parent, pos, rectSize);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.text = text;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[GameOverSummarySetup] {target.GetType().Name}.{property} not found.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
