using TinyRoadTrip.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-click, idempotent setup for the pause button and pause screen.
// Builds the PauseUI canvas only if it is missing; always (re)wires PauseManager.
public static class PauseMenuSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string LabelFontPath = "Assets/Fonts Assets/LuckiestGuy-Regular SDF.asset";
    private const string TitleFontPath = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Oswald Bold SDF.asset";
    private const string PanelSprite = "Assets/Models/UI Sprites/Group 6.png";
    private const string ButtonSprite = "Assets/Models/Kenny UI Pack/PNG/Yellow/Double/button_square_gloss.png";
    private const string RoundSprite = "Assets/Models/Kenny UI Pack/PNG/Blue/Default/button_round_gloss.png";

    private static readonly Color TitleColor = new Color(0.642f, 0.316f, 0.088f); // matches GAME OVER
    private static readonly Color LabelColor = new Color(0.196f, 0.196f, 0.196f); // matches RESTART

    [MenuItem("Tools/Tiny Road Trip/Set Up Pause Menu")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError($"[PauseMenuSetup] Open {ScenePath} first (active: {scene.path}).");
            return;
        }

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        GameObject systems = GameObject.Find("GameplaySystems");
        if (gameManager == null || player == null || systems == null)
        {
            Debug.LogError("[PauseMenuSetup] Needs GameManager, PlayerController and GameplaySystems. Run Set Up Collectibles first.");
            return;
        }

        PauseManager pause = systems.GetComponent<PauseManager>() ?? systems.AddComponent<PauseManager>();

        GameObject ui = GameObject.Find("PauseUI");
        if (ui == null) ui = BuildUI(pause);
        EnsureCloseHotspot(ui.transform.Find("PausePanel"), pause);

        Transform safe = ui.transform.Find("SafeArea");
        Set(pause, "gameManager", gameManager);
        Set(pause, "player", player);
        Set(pause, "pauseButton", safe != null ? safe.Find("PauseButton")?.gameObject : null);
        Set(pause, "pausePanel", ui.transform.Find("PausePanel")?.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PauseMenuSetup] Done: pause button, pause screen and PauseManager set up.");
    }

    private static GameObject BuildUI(PauseManager pause)
    {
        TMP_FontAsset labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFontPath);
        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath) ?? labelFont;

        var canvasGo = new GameObject("PauseUI", typeof(RectTransform));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // above the score and HUD
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Pause button (top-right, inside the safe area).
        RectTransform safe = Rect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        RectTransform pauseButton = Rect("PauseButton", safe, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -24f), new Vector2(120f, 120f));
        Image bg = Img(pauseButton, LoadSprite(RoundSprite), Color.white, true);
        Button pb = pauseButton.gameObject.AddComponent<Button>();
        pb.targetGraphic = bg;
        UnityEventTools.AddPersistentListener(pb.onClick, new UnityAction(pause.Pause));
        foreach (float x in new[] { -15f, 15f })
        {
            RectTransform bar = Rect(x < 0 ? "BarLeft" : "BarRight", pauseButton, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(x, 2f), new Vector2(17f, 52f));
            Img(bar, null, Color.white, false);
        }

        // Pause screen (hidden until paused).
        RectTransform panelRoot = Rect("PausePanel", canvasGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform dim = Rect("Dim", panelRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Img(dim, null, new Color(0f, 0f, 0f, 0.55f), true); // also blocks clicks behind the menu

        Vector2 panelSize = new Vector2(905f, 803f); // same size as the Game Over panel
        RectTransform panel = Rect("Panel", panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);
        Img(panel, LoadSprite(PanelSprite), Color.white, false);

        float half = panelSize.y * 0.5f;
        Text("Title", panelRoot, titleFont, 100, TitleColor, new Vector2(0f, half - 105f), new Vector2(800f, 120f), "PAUSED");

        MenuButton(panelRoot, labelFont, "ResumeButton", "RESUME", new Vector2(0f, 75f), new Vector2(560f, 150f), 100, pause.Resume);
        MenuButton(panelRoot, labelFont, "RestartButton", "RESTART", new Vector2(0f, -100f), new Vector2(500f, 120f), 72, pause.Restart);
        MenuButton(panelRoot, labelFont, "MenuButton", "MAIN MENU", new Vector2(0f, -250f), new Vector2(500f, 120f), 64, pause.MainMenu);

        panelRoot.gameObject.SetActive(false);
        return canvasGo;
    }

    // The panel art (Group 6) has a close "X" baked into its bottom-right corner, as on the Game Over panel.
    // An invisible button over it resumes the game.
    private static void EnsureCloseHotspot(Transform panelRoot, PauseManager pause)
    {
        if (panelRoot == null || panelRoot.Find("CloseHotspot") != null) return;
        RectTransform rt = Rect("CloseHotspot", panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(366f, -312f), new Vector2(140f, 140f));
        Image hit = Img(rt, null, new Color(1f, 1f, 1f, 0f), true);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = hit;
        b.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(pause.Resume));
    }

    private static void MenuButton(Transform parent, TMP_FontAsset font, string name, string label, Vector2 pos, Vector2 size,
        float fontSize, UnityAction onClick)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        Image img = Img(rt, LoadSprite(ButtonSprite), Color.white, true);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        TMP_Text t = Text("Label", rt, font, fontSize, LabelColor, new Vector2(0f, 8f), size, label);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.sizeDelta = Vector2.zero;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 rectSize, string text)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, rectSize);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.text = text;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[PauseMenuSetup] Sprite not found: {path}");
        return s;
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[PauseMenuSetup] {target.GetType().Name}.{property} not found.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
