using System.IO;
using TinyRoadTrip.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-click, idempotent scene setup for collectibles, power-ups, near-miss combo and the HUD.
// Creates only what is missing (assets, prefab, scene objects, HUD) and (re)wires references.
public static class CollectiblesSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DataFolder = "Assets/Data/Collectibles";
    private const string PrefabFolder = "Assets/Prefabs/Collectibles";
    private const string PrefabPath = PrefabFolder + "/Collectible.prefab";
    private const string FontPath = "Assets/Fonts Assets/LuckiestGuy-Regular SDF.asset";
    private const string KenneyPng = "Assets/Models/Kenny UI Pack/PNG/";
    private const string CoinSprite = "Assets/Models/Kenny Models/Previews/item-coin-gold.png";

    [MenuItem("Tools/Tiny Road Trip/Set Up Collectibles")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError($"[CollectiblesSetup] Open {ScenePath} first (active: {scene.path}).");
            return;
        }

        CollectibleDefinition coin = EnsureDefinition("Coin", CollectibleKind.Coin, d =>
        {
            d.displayName = "COIN"; d.spawnWeight = 70f; d.coinValue = 1; d.duration = 0f; d.magnitude = 0f;
            d.sprite = LoadSprite(CoinSprite); d.worldSize = 0.55f; d.uiColor = new Color(1f, 0.84f, 0.2f);
        });
        CollectibleDefinition slow = EnsureDefinition("SlowTime", CollectibleKind.SlowTime, d =>
        {
            d.displayName = "SLOW-MO"; d.spawnWeight = 6f; d.duration = 5f; d.magnitude = 0.5f;
            d.sprite = LoadSprite(KenneyPng + "Blue/Default/button_round_gloss.png");
            d.icon = LoadSprite(KenneyPng + "Extra/Default/icon_repeat_light.png");
            d.worldSize = 0.75f; d.uiColor = new Color(0.35f, 0.65f, 1f);
            d.pickupSound = "Whoosh"; d.pickupMessage = "SLOW-MO!";
        });
        CollectibleDefinition shield = EnsureDefinition("Shield", CollectibleKind.Shield, d =>
        {
            d.displayName = "SHIELD"; d.spawnWeight = 6f; d.duration = 0f; d.magnitude = 0f;
            d.sprite = LoadSprite(KenneyPng + "Green/Default/button_round_gloss.png");
            d.icon = LoadSprite(KenneyPng + "Grey/Default/icon_checkmark.png");
            d.worldSize = 0.75f; d.uiColor = new Color(0.3f, 0.9f, 0.45f);
            d.pickupSound = "Whoosh"; d.pickupMessage = "SHIELD!";
        });
        CollectibleDefinition nitro = EnsureDefinition("Nitro", CollectibleKind.Nitro, d =>
        {
            d.displayName = "NITRO"; d.spawnWeight = 12f; d.duration = 4f; d.magnitude = 1.5f;
            d.sprite = LoadSprite(KenneyPng + "Red/Default/button_round_gloss.png");
            d.icon = LoadSprite(KenneyPng + "Extra/Default/icon_arrow_up_light.png");
            d.worldSize = 0.75f; d.uiColor = new Color(1f, 0.45f, 0.15f);
            d.pickupSound = "Whoosh"; d.pickupMessage = "NITRO!";
        });
        CollectibleDefinition magnet = EnsureDefinition("CoinMagnet", CollectibleKind.CoinMagnet, d =>
        {
            d.displayName = "MAGNET"; d.spawnWeight = 6f; d.duration = 8f; d.magnitude = 3f;
            d.sprite = LoadSprite(KenneyPng + "Grey/Default/button_round_gloss.png"); d.tint = new Color(1f, 0.45f, 0.85f);
            d.icon = LoadSprite(KenneyPng + "Extra/Default/icon_arrow_down_light.png");
            d.worldSize = 0.75f; d.uiColor = new Color(1f, 0.45f, 0.85f);
            d.pickupSound = "Whoosh"; d.pickupMessage = "MAGNET!";
        });

        Collectible prefab = EnsurePrefab();

        // Existing scene objects
        Spawner obstacleSpawner = Object.FindFirstObjectByType<Spawner>();
        BackgroundScroller road = Object.FindFirstObjectByType<BackgroundScroller>();
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        ScoreBridge scoreBridge = Object.FindFirstObjectByType<ScoreBridge>();
        GameObject player = GameObject.FindWithTag("Player");
        if (obstacleSpawner == null || road == null || gameManager == null || scoreBridge == null || player == null)
        {
            Debug.LogError("[CollectiblesSetup] Missing Spawner / BackgroundScroller / GameManager / ScoreBridge / Player in scene.");
            return;
        }
        PlayerCollision playerCollision = player.GetComponent<PlayerCollision>();
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        SpriteRenderer playerSprite = player.GetComponent<SpriteRenderer>();

        // Player visuals
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        SpriteRenderer bubble = EnsurePlayerVisual(player, "ShieldBubble", knob, new Color(0.35f, 1f, 0.6f, 0.35f), new Vector2(2.0f, 2.0f), Vector2.zero, 1);
        SpriteRenderer flame = EnsurePlayerVisual(player, "NitroFlame", knob, new Color(1f, 0.55f, 0.1f, 0.85f), new Vector2(0.45f, 0.9f), new Vector2(0f, -1.25f), 0, 0.02f);
        SpriteRenderer ring = EnsurePlayerVisual(player, "MagnetRing", LoadSprite(KenneyPng + "Grey/Default/button_round_line.png"), new Color(1f, 0.45f, 0.85f, 0.45f), new Vector2(2.4f, 2.4f), Vector2.zero, 1);

        // Systems
        GameObject systems = FindOrCreate("GameplaySystems", null);
        GameplaySpeedController speed = GetOrAdd<GameplaySpeedController>(systems);
        CoinManager coinManager = GetOrAdd<CoinManager>(systems);
        PowerUpManager powerUps = GetOrAdd<PowerUpManager>(systems);
        SlowTimeEffect slowFx = GetOrAdd<SlowTimeEffect>(systems);
        ShieldEffect shieldFx = GetOrAdd<ShieldEffect>(systems);
        NitroEffect nitroFx = GetOrAdd<NitroEffect>(systems);
        CoinMagnetEffect magnetFx = GetOrAdd<CoinMagnetEffect>(systems);
        ComboManager combo = GetOrAdd<ComboManager>(systems);
        NearMissDetector nearMiss = GetOrAdd<NearMissDetector>(systems);
        GameObject poolGo = FindOrCreate("CollectiblePool", systems.transform);
        CollectibleSpawner collectibleSpawner = GetOrAdd<CollectibleSpawner>(poolGo);

        Set(obstacleSpawner, "speedController", speed);
        Set(road, "speedController", speed);
        Set(coinManager, "gameManager", gameManager);
        SetArray(powerUps, "effects", new Object[] { slowFx, shieldFx, nitroFx, magnetFx });
        Set(slowFx, "speedController", speed);
        Set(nitroFx, "speedController", speed);
        Set(nitroFx, "scoreBridge", scoreBridge);
        Set(nitroFx, "flame", flame);
        Set(shieldFx, "obstacleSpawner", obstacleSpawner);
        Set(shieldFx, "playerBody", playerBody);
        Set(shieldFx, "playerCollider", playerCollider);
        Set(shieldFx, "playerSprite", playerSprite);
        Set(shieldFx, "bubble", bubble);
        Set(magnetFx, "ring", ring);
        Set(combo, "scoreBridge", scoreBridge);
        Set(combo, "gameManager", gameManager);
        Set(nearMiss, "obstacleSpawner", obstacleSpawner);
        Set(nearMiss, "playerCollider", playerCollider);
        Set(nearMiss, "comboManager", combo);
        Set(collectibleSpawner, "obstacleSpawner", obstacleSpawner);
        Set(collectibleSpawner, "road", road);
        Set(collectibleSpawner, "speedController", speed);
        Set(collectibleSpawner, "player", player.transform);
        Set(collectibleSpawner, "coinManager", coinManager);
        Set(collectibleSpawner, "powerUpManager", powerUps);
        Set(collectibleSpawner, "collectiblePrefab", prefab);
        SetArray(collectibleSpawner, "definitions", new Object[] { coin, slow, shield, nitro, magnet });
        if (playerCollision != null)
        {
            Set(playerCollision, "gameManager", gameManager);
            Set(playerCollision, "powerUps", powerUps);
            Set(playerCollision, "combo", combo);
        }

        EnsureHud(coinManager, powerUps, combo, collectibleSpawner, coin, new[] { slow, shield, nitro, magnet });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CollectiblesSetup] Done: assets, prefab, scene objects and HUD are set up and wired.");
    }

    // ---------- assets ----------

    private static CollectibleDefinition EnsureDefinition(string name, CollectibleKind kind, System.Action<CollectibleDefinition> initialise)
    {
        EnsureFolder(DataFolder);
        string path = $"{DataFolder}/Collectible_{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CollectibleDefinition>(path);
        if (existing != null) return existing; // keep designer tuning

        var d = ScriptableObject.CreateInstance<CollectibleDefinition>();
        d.kind = kind;
        initialise(d);
        AssetDatabase.CreateAsset(d, path);
        return d;
    }

    private static Collectible EnsurePrefab()
    {
        EnsureFolder(PrefabFolder);
        var existing = AssetDatabase.LoadAssetAtPath<Collectible>(PrefabPath);
        if (existing != null) return existing;

        var root = new GameObject("Collectible");
        var body = root.AddComponent<SpriteRenderer>();
        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var circle = root.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.32f;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(root.transform, false);
        iconGo.transform.localScale = Vector3.one * 0.55f;
        iconGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        var icon = iconGo.AddComponent<SpriteRenderer>();

        var collectible = root.AddComponent<Collectible>();
        Set(collectible, "body", body);
        Set(collectible, "icon", icon);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved.GetComponent<Collectible>();
    }

    // ---------- scene ----------

    private static SpriteRenderer EnsurePlayerVisual(GameObject player, string name, Sprite sprite, Color color,
        Vector2 worldSize, Vector2 worldOffset, int sortingOrder, float worldZBehind = -0.02f)
    {
        Transform t = player.transform.Find(name);
        if (t != null) return t.GetComponent<SpriteRenderer>();

        var go = new GameObject(name);
        go.transform.SetParent(player.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;

        Vector3 parentScale = player.transform.lossyScale;
        Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        go.transform.localScale = new Vector3(
            worldSize.x / (spriteSize.x * parentScale.x),
            worldSize.y / (spriteSize.y * parentScale.y),
            1f);
        go.transform.localPosition = new Vector3(worldOffset.x / parentScale.x, worldOffset.y / parentScale.y, worldZBehind / parentScale.z);
        return sr;
    }

    private static void EnsureHud(CoinManager coins, PowerUpManager powerUps, ComboManager combo, CollectibleSpawner spawner,
        CollectibleDefinition coinDef, CollectibleDefinition[] powerUpDefs)
    {
        GameObject existing = GameObject.Find("CollectiblesHUD");
        CollectiblesHUD hud;
        if (existing != null)
        {
            hud = existing.GetComponent<CollectiblesHUD>();
        }
        else
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canvasGo = new GameObject("CollectiblesHUD", typeof(RectTransform));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1; // behind the existing score and Game Over canvases
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the HUD is display-only and must never block touches.

            RectTransform safe = Rect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Top row container; CollectiblesHUD moves it below the score's glyphs at runtime.
            RectTransform top = Rect("TopContent", safe, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(0, 440));

            // Coins (top-left)
            RectTransform coinPanel = Rect("CoinPanel", top, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, 0), new Vector2(280, 140));
            Panel(coinPanel);
            RectTransform coinIcon = Rect("CoinIcon", coinPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(60, -52), new Vector2(84, 84));
            Img(coinIcon, coinDef.sprite, Color.white);
            TMP_Text run = Text("RunCoins", coinPanel, font, 70, TextAlignmentOptions.MidlineLeft, Color.white,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -8), new Vector2(160, 88), "0");
            TMP_Text total = Text("TotalCoins", coinPanel, font, 30, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.9f, 0.55f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -96), new Vector2(250, 38), "BANK 0");

            // Power-ups (top-right)
            RectTransform list = Rect("PowerUps", top, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, 0), new Vector2(320, 420));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 12;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var slots = new CollectiblesHUD.PowerUpSlot[powerUpDefs.Length];
            for (int i = 0; i < powerUpDefs.Length; i++)
            {
                CollectibleDefinition d = powerUpDefs[i];
                RectTransform slot = Rect("Slot_" + d.kind, list, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(320, 90));
                Panel(slot);
                RectTransform icon = Rect("Icon", slot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(48, 0), new Vector2(72, 72));
                Image iconImg = Img(icon, d.sprite, d.tint);
                RectTransform glyph = Rect("Glyph", icon, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-34, -34));
                Img(glyph, d.icon, Color.white).preserveAspect = true;
                TMP_Text label = Text("Label", slot, font, 32, TextAlignmentOptions.MidlineLeft, Color.white,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -6), new Vector2(210, 40), d.displayName);
                RectTransform barBg = Rect("Bar", slot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(96, 14), new Vector2(205, 22));
                Img(barBg, null, new Color(0f, 0f, 0f, 0.55f));
                RectTransform fill = Rect("Fill", barBg, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                Image fillImg = Img(fill, null, d.uiColor);
                slots[i] = new CollectiblesHUD.PowerUpSlot
                {
                    kind = d.kind, root = slot.gameObject, icon = iconImg, fill = fill, fillImage = fillImg, label = label,
                };
            }

            // Popup (screen centre: clear of the top row on every aspect ratio)
            TMP_Text popup = Text("Popup", safe, font, 88, TextAlignmentOptions.Center, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 160), "");

            hud = canvasGo.AddComponent<CollectiblesHUD>();
            Set(hud, "runCoinsText", run);
            Set(hud, "totalCoinsText", total);
            Set(hud, "coinIcon", coinIcon);
            Set(hud, "popupText", popup);
            var so = new SerializedObject(hud);
            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty s = slotsProp.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("kind").enumValueIndex = (int)slots[i].kind;
                s.FindPropertyRelative("root").objectReferenceValue = slots[i].root;
                s.FindPropertyRelative("icon").objectReferenceValue = slots[i].icon;
                s.FindPropertyRelative("fill").objectReferenceValue = slots[i].fill;
                s.FindPropertyRelative("fillImage").objectReferenceValue = slots[i].fillImage;
                s.FindPropertyRelative("label").objectReferenceValue = slots[i].label;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (hud != null)
        {
            Set(hud, "coinManager", coins);
            Set(hud, "powerUpManager", powerUps);
            Set(hud, "comboManager", combo);
            Set(hud, "collectibleSpawner", spawner);

            Transform safe = hud.transform.Find("SafeArea");
            if (safe != null)
            {
                Set(hud, "safeArea", safe.GetComponent<SafeAreaFitter>());
                Transform top = safe.Find("TopContent");
                if (top != null) Set(hud, "topContent", top);
            }
            GameObject score = GameObject.Find("ScoreUI");
            if (score != null) Set(hud, "placeBelow", score.GetComponent<TMP_Text>());
        }
    }

    // ---------- helpers ----------

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

    private static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null) img.preserveAspect = true;
        return img;
    }

    private static void Panel(RectTransform rt)
    {
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);
        bg.raycastTarget = false;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 rectSize, string text)
    {
        RectTransform rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, rectSize);
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

    private static GameObject FindOrCreate(string name, Transform parent)
    {
        GameObject go = parent != null ? parent.Find(name)?.gameObject : GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[CollectiblesSetup] {target.GetType().Name}.{property} not found.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[CollectiblesSetup] Sprite not found: {path} (placeholder left empty).");
        return s;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
