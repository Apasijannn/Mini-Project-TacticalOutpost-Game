using System.Collections;
using Outpost.Enemy;
using Outpost.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Outpost.UI
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameMenu : MonoBehaviour
    {
        public const string MenuScene = "Assets/Scenes/Gameplay/TacticalOutpost_Menu.unity";
        public const string GameScene = "Assets/Scenes/Gameplay/TacticalOutpost_Game.unity";
        public enum Page { Home, Settings, Controls, FieldManual }
        public static GameMenu Instance { get; private set; }
        public bool mainMenu;
        public Sprite primaryButton, secondaryButton, panelSprite;
        public Font font;
        public TMP_FontAsset headingFont, bodyFont, monoFont;
        public ManualEntry[] manualEntries;
        public Vector3[] previewSpawns, previewTargets;
        public bool IsPaused { get; private set; }
        public Page CurrentPage { get; private set; }
        public static readonly Color Ivory = new Color(.91f, .91f, .84f);
        public static readonly Color Muted = new Color(.61f, .65f, .61f);
        public static readonly Color Amber = new Color(.96f, .67f, .25f);
        public static readonly Color Panel = new Color(.035f, .05f, .052f, .96f);
        GameObject overlay, home, settings, controls, manual, loadingScreen;
        RectTransform frame;
        CanvasGroup content;
        TMP_Text loadingLabel, settingsFeedback, qualityLabel;
        UnityEngine.UI.Slider volumeSlider, sensitivitySlider, fovSlider;
        UnityEngine.UI.Toggle bobToggle, fullscreenToggle;
        UnityEngine.UI.Button firstButton;
        int selectedQuality;
        bool loading;
        MenuBackdrop backdrop;
        Texture2D gradient;
        AudioSource uiAudio;
        AudioClip click;
        GameObject briefing, dossier;
        FieldManualViewer manualViewer;
        TMP_Text dossierName, dossierDescription, dossierStats, dossierStatus, dossierCounter;
        bool showWeapons;
        int dossierIndex;

        void Awake() { Instance = this; GameOptions.ApplyGlobal(); }
        void Start()
        {
            BuildUi(); overlay.SetActive(mainMenu);
            if (mainMenu)
            {
                Time.timeScale = 1f; AudioListener.pause = false; FpsController.LockCursor(false);
                backdrop = gameObject.AddComponent<MenuBackdrop>();
                backdrop.entries = manualEntries; backdrop.spawns = previewSpawns; backdrop.targets = previewTargets;
                content.alpha = 0; content.interactable = false;
                loadingScreen.SetActive(true); loadingLabel.text = "ESTABLISHING UPLINK...";
            }
        }
        void Update()
        {
            if (!mainMenu || loading) return;
            if (backdrop != null && backdrop.Ready)
            {
                loadingScreen.SetActive(false);
                content.alpha = Mathf.MoveTowards(content.alpha, 1f, Time.unscaledDeltaTime * 1.3f);
                content.interactable = true;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && CurrentPage != Page.Home) ShowPage(Page.Home);
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f; AudioListener.pause = false; AudioListener.volume = GameOptions.Volume;
            if (gradient != null) Destroy(gradient);
            if (click != null) Destroy(click);
        }
        public void TogglePause()
        {
            if (mainMenu || loading || overlay == null) return;
            var hp = PlayerHealth.Instance; var wave = WaveDirector.Instance;
            if (hp == null || hp.IsDead || (wave != null && (wave.CurrentPhase == WaveDirector.Phase.Defeat || wave.CurrentPhase == WaveDirector.Phase.Victory))) return;
            if (IsPaused)
            {
                if (CurrentPage != Page.Home) ShowPage(Page.Home); else Resume();
                return;
            }
            IsPaused = true; overlay.SetActive(true); ShowPage(Page.Home);
            hp.GetComponent<FpsController>().InputEnabled = false;
            Time.timeScale = 0f; AudioListener.pause = true; FpsController.LockCursor(false);
        }
        public void Resume()
        {
            if (!IsPaused || loading) return;
            AudioListener.volume = GameOptions.Volume;
            IsPaused = false; overlay.SetActive(false); Time.timeScale = 1f; AudioListener.pause = false;
            StartCoroutine(RestoreControls());
        }
        IEnumerator RestoreControls()
        {
            // The click that selects Resume must not fire the weapon.
            yield return null;
            if (IsPaused || loading || PlayerHealth.Instance == null || PlayerHealth.Instance.IsDead) yield break;
            PlayerHealth.Instance.GetComponent<FpsController>().InputEnabled = true; FpsController.LockCursor(true);
        }
        public void StartGame() { if (!loading) StartCoroutine(LoadScene(GameScene)); }
        public void Exit()
        {
            if (loading) return;
            if (!mainMenu) { StartCoroutine(LoadScene(MenuScene)); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        IEnumerator LoadScene(string path)
        {
            loading = true; content.interactable = false;
            Time.timeScale = 1f; AudioListener.pause = false; AudioListener.volume = GameOptions.Volume; FpsController.LockCursor(false);
            loadingLabel.text = path == GameScene ? "DEPLOYING TO THE OUTPOST..." : "RETURNING TO COMMAND...";
            loadingScreen.SetActive(true); yield return null;
            yield return SceneManager.LoadSceneAsync(path);
        }
        public void ShowPage(Page page)
        {
            if (CurrentPage == Page.Settings) AudioListener.volume = GameOptions.Volume;
            CurrentPage = page;
            home.SetActive(page == Page.Home); settings.SetActive(page == Page.Settings);
            controls.SetActive(page == Page.Controls); manual.SetActive(page == Page.FieldManual);
            if (page == Page.Settings) RefreshSettings();
            if (EventSystem.current != null)
            {
                var selected = page == Page.Home ? (UnityEngine.UI.Selectable)firstButton :
                    page == Page.Settings ? volumeSlider : (page == Page.Controls ? controls : manual).GetComponentInChildren<UnityEngine.UI.Selectable>();
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }
        }

        RectTransform At(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        UnityEngine.UI.Image Box(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = At(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        TMP_Text Label(string name, Transform parent, string text, float x, float y, float width, float height, float size, Color color,
            TMP_FontAsset face = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var label = At(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = face != null ? face : bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; label.color = color; label.alignment = align;
            label.raycastTarget = false; label.overflowMode = TextOverflowModes.Truncate; return label;
        }
        UnityEngine.UI.Button Button(string name, Transform parent, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var image = Box(name, parent, x, y, width, height, Color.white); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = primary ? Amber : new Color(.085f, .11f, .11f, .92f);
            colors.highlightedColor = primary ? new Color(1f, .79f, .42f) : new Color(.20f, .25f, .24f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? new Color(.75f, .46f, .14f) : new Color(.29f, .34f, .31f);
            colors.fadeDuration = .12f; button.colors = colors;
            Box("Edge", image.transform, 0, 0, primary ? 5 : 2, height, primary ? Ivory : Amber);
            Label("Label", image.transform, text, 24, 0, width - 42, height, height > 60 ? 29 : 22,
                primary ? new Color(.055f, .07f, .065f) : Ivory, headingFont, TextAlignmentOptions.MidlineLeft);
            button.onClick.AddListener(() => { if (uiAudio != null) uiAudio.PlayOneShot(click, .25f); action(); }); return button;
        }
        void BuildUi()
        {
            if (EventSystem.current == null) new GameObject("Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            overlay = new GameObject("Menu overlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var shadeRect = At("Backdrop", overlay.transform, 0, 0, 0, 0);
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one; shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            var background = shadeRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false); gradient.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                float alpha = mainMenu ? Mathf.Lerp(.97f, .03f, Mathf.SmoothStep(0, 1, t * 1.35f)) : .86f;
                gradient.SetPixel(i, 0, new Color(.015f, .025f, .027f, alpha));
            }
            gradient.Apply(); background.texture = gradient; background.raycastTarget = false;
            frame = At("Command interface", overlay.transform, 0, 0, 1920, 1080);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            content = frame.gameObject.AddComponent<CanvasGroup>();
            Label("Operation", frame, "OUTPOST COMMAND  /  OPERATIONS", 76, 68, 720, 30, 17, Amber, monoFont);
            Box("Header rule", frame, 76, 114, 1768, 1, new Color(.7f, .75f, .7f, .23f));
            Box("Link light", frame, 1480, 76, 7, 7, new Color(.4f, .75f, .58f));
            Label("Sector", frame, "INDUSTRIAL SECTOR / SOLO DEFENSE", 1500, 69, 344, 25, 14, Muted, monoFont, TextAlignmentOptions.TopRight);
            Label("Title top", frame, mainMenu ? "" : "MISSION", 76, 165, 510, 66, 40, Ivory, headingFont);
            Label("Title", frame, mainMenu ? "BLACKOUT" : "PAUSED", 70, 232, 550, 140, 112, Ivory, headingFont);
            Box("Title accent", frame, 76, 375, 54, 4, Amber);
            Label("Subtitle", frame, mainMenu ? "KEEP THE POWER.\nHOLD THE LINE." : "Take a breath.\nThe outpost needs you.", 76, 397, 480, 75, 27, Muted);
            float y = 505;
            firstButton = Button(mainMenu ? "START" : "RESUME", frame, mainMenu ? "START OPERATION" : "RESUME OPERATION", 76, y, 455, 76,
                mainMenu ? (UnityEngine.Events.UnityAction)StartGame : Resume, true);
            Button("SETTINGS", frame, "SETTINGS", 76, y + 94, 455, 64, () => ShowPage(Page.Settings));
            Button("CONTROLS", frame, "CONTROLS", 76, y + 170, 455, 64, () => ShowPage(Page.Controls));
            Button("FIELD MANUAL", frame, "FIELD MANUAL", 76, y + 246, 455, 64, () => ShowPage(Page.FieldManual));
            Button("EXIT", frame, mainMenu ? "EXIT GAME" : "MAIN MENU", 76, y + 322, 455, 64, Exit);
            Label("Footer", frame, "TACTICAL OUTPOST / INTELLIGENT ASSAULT AI", 76, 1000, 950, 30, 14, Muted, monoFont);
            Label("Build", frame, "BUILD " + Application.version + "   //   STAND YOUR GROUND", 1130, 1000, 714, 30, 14, Muted, monoFont, TextAlignmentOptions.TopRight);
            BuildHome(); BuildSettings(); BuildControls(); BuildManual();
            loadingScreen = Box("Deployment screen", overlay.transform, 0, 0, 0, 0, new Color(.015f, .025f, .027f, .98f)).gameObject;
            var loadRect = (RectTransform)loadingScreen.transform; loadRect.anchorMin = Vector2.zero; loadRect.anchorMax = Vector2.one;
            loadRect.offsetMin = loadRect.offsetMax = Vector2.zero;
            var loadingFrame = At("Loading content", loadingScreen.transform, 0, 0, 900, 180);
            loadingFrame.anchorMin = loadingFrame.anchorMax = loadingFrame.pivot = new Vector2(.5f, .5f); loadingFrame.anchoredPosition = Vector2.zero;
            Label("Loading title", loadingFrame, "OPERATION BLACKOUT", 0, 0, 900, 100, 68, Ivory, headingFont, TextAlignmentOptions.Center);
            loadingLabel = Label("Loading status", loadingFrame, "DEPLOYING...", 0, 110, 900, 50, 20, Amber, monoFont, TextAlignmentOptions.Center);
            loadingScreen.SetActive(false);
            uiAudio = gameObject.AddComponent<AudioSource>(); uiAudio.playOnAwake = false; uiAudio.ignoreListenerPause = true;
            click = AudioClip.Create("Command confirm", 1600, 1, 22050, false); var samples = new float[1600];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * .22f) * Mathf.Sin(Mathf.PI * i / samples.Length) * .16f;
            click.SetData(samples, 0); ShowPage(Page.Home);
        }
        void BuildHome()
        {
            home = At("Home", frame, 0, 0, 1920, 1080).gameObject;
            var card = Box("Operation brief", home.transform, 1280, 760, 564, 190, new Color(.025f, .04f, .044f, .88f));
            Box("Brief accent", card.transform, 0, 0, 564, 2, Amber);
            Label("Brief category", card.transform, "OPERATION BRIEF  /  SOLO DEFENSE", 28, 24, 510, 28, 15, Amber, monoFont);
            Label("Brief title", card.transform, mainMenu ? "ONE OUTPOST. ONE DEFENDER." : "THE FIGHT IS ON HOLD.", 28, 60, 510, 48, 32, Ivory, headingFont);
            Label("Brief body", card.transform, mainMenu ? "Protect the power station. Survive the assault.\nMake every round count." : "Review your controls or adjust your settings.\nResume when you're ready.", 28, 111, 510, 60, 23, Muted);
        }
        RectTransform PagePanel(string name, string category, string title, string summary)
        {
            var panel = Box(name, frame, 620, 174, 1224, 776, Panel).rectTransform;
            Box("Top edge", panel, 0, 0, 1224, 2, Amber);
            Label("Category", panel, category, 40, 30, 850, 25, 15, Amber, monoFont);
            Label("Heading", panel, title, 38, 66, 1020, 66, 52, Ivory, headingFont);
            Label("Summary", panel, summary, 40, 140, 1130, 42, 24, Muted);
            Button("BACK", panel, "BACK / ESC", 1010, 30, 174, 45, () => ShowPage(Page.Home)); return panel;
        }
        void BuildSettings()
        {
            var panel = PagePanel("Settings", "OPTIONS", "SETTINGS", ""); settings = panel.gameObject;
            volumeSlider = SettingSlider(panel, "MASTER VOLUME", 215, 0, 1, value => (value * 100).ToString("F0") + "%");
            sensitivitySlider = SettingSlider(panel, "MOUSE SENSITIVITY", 335, .04f, .3f, value => (value / .11f).ToString("F2") + "x");
            fovSlider = SettingSlider(panel, "FIELD OF VIEW", 455, 60, 100, value => value.ToString("F0") + " deg");
            fovSlider.wholeNumbers = true; volumeSlider.onValueChanged.AddListener(value => AudioListener.volume = value);
            var display = Box("Display options", panel, 720, 214, 464, 348, new Color(.08f, .11f, .11f, .7f));
            Label("Display category", display.transform, "DISPLAY", 24, 24, 420, 30, 17, Amber, monoFont);
            bobToggle = SettingToggle(display.transform, "HEAD BOB", 75);
            fullscreenToggle = SettingToggle(display.transform, "FULLSCREEN", 155);
            var quality = Button("QUALITY", display.transform, "", 24, 253, 416, 64, () => {
                selectedQuality = (selectedQuality + 1) % QualitySettings.names.Length; UpdateQuality();
            }); qualityLabel = quality.GetComponentInChildren<TMP_Text>();
            Button("RESET DEFAULTS", panel, "RESTORE DEFAULTS", 40, 677, 286, 58, ResetSettings);
            Button("APPLY SETTINGS", panel, "APPLY", 870, 677, 314, 58, ApplySettings, true);
            settingsFeedback = Label("Settings status", panel, "", 350, 685, 495, 44, 21, Muted, bodyFont, TextAlignmentOptions.MidlineRight);
        }
        UnityEngine.UI.Slider SettingSlider(Transform parent, string title, float y, float min, float max, System.Func<float, string> format)
        {
            Label(title, parent, title, 40, y, 560, 32, 22, Ivory, headingFont);
            var value = Label(title + " value", parent, "", 550, y, 110, 35, 22, Amber, monoFont, TextAlignmentOptions.TopRight);
            var rect = At(title + " slider", parent, 40, y + 45, 620, 24);
            var hit = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear;
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.minValue = min; slider.maxValue = max;
            Box("Track", rect, 0, 9, 620, 5, new Color(.25f, .3f, .28f));
            var fillArea = At("Fill area", rect, 0, 9, 620, 5); var fill = Box("Fill", fillArea, 0, 0, 620, 5, Amber);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = At("Handle area", rect, 7, 0, 606, 24); var handle = Box("Handle", handleArea, 0, 0, 14, 24, Ivory);
            handle.raycastTarget = true; handle.rectTransform.pivot = new Vector2(.5f, .5f); handle.rectTransform.sizeDelta = new Vector2(14, 0);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(v => value.text = format(v)); value.text = format(slider.value); return slider;
        }
        UnityEngine.UI.Toggle SettingToggle(Transform parent, string title, float y)
        {
            var row = At(title, parent, 24, y, 416, 62); var hit = row.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear;
            var box = Box("Checkbox", row, 0, 5, 28, 28, new Color(.19f, .24f, .22f)); box.raycastTarget = true;
            var mark = Box("Check", box.transform, 6, 6, 16, 16, Amber);
            var toggle = row.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = box; toggle.graphic = mark;
            Label("Title", row, title, 45, 0, 370, 30, 22, Ivory, headingFont); return toggle;
        }
        void UpdateQuality() { qualityLabel.text = "QUALITY / " + QualitySettings.names[selectedQuality].ToUpperInvariant(); }
        void RefreshSettings()
        {
            volumeSlider.value = GameOptions.Volume; sensitivitySlider.value = GameOptions.Sensitivity; fovSlider.value = GameOptions.FieldOfView;
            bobToggle.isOn = GameOptions.HeadBob; fullscreenToggle.isOn = GameOptions.Fullscreen;
            selectedQuality = GameOptions.Quality; UpdateQuality(); settingsFeedback.text = "";
        }
        void ResetSettings()
        {
            volumeSlider.value = 1; sensitivitySlider.value = .11f; fovSlider.value = 70; bobToggle.isOn = true; fullscreenToggle.isOn = Screen.fullScreen;
            selectedQuality = QualitySettings.GetQualityLevel(); UpdateQuality(); settingsFeedback.text = "Defaults restored. Apply to save.";
        }
        public void ApplySettings()
        {
            GameOptions.Save(volumeSlider.value, sensitivitySlider.value, fovSlider.value, bobToggle.isOn, selectedQuality, fullscreenToggle.isOn);
            settingsFeedback.text = "Settings saved.";
        }
        void BuildControls()
        {
            var panel = PagePanel("Controls", "OPERATOR CONTROLS", "KNOW YOUR EQUIPMENT", "Your controls at a glance. Highlighted keys are active bindings."); controls = panel.gameObject;
            var keyboard = At("Keyboard diagram", panel, 40, 224, 860, 300);
            KeyRow(keyboard, 0, "ESC F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12", 55);
            KeyRow(keyboard, 54, "~ 1 2 3 4 5 6 7 8 9 0 - = BKSP", 52);
            KeyRow(keyboard, 103, "TAB Q W E R T Y U I O P [ ] |", 52);
            KeyRow(keyboard, 152, "CAPS A S D F G H J K L ; ' ENTER", 52);
            KeyRow(keyboard, 201, "LSHIFT Z X C V B N M , . / RSHIFT", 52);
            KeyRow(keyboard, 250, "LCTRL WIN ALT SPACE ALT WIN MENU RCTRL", 52);
            var mouse = Box("Mouse diagram", panel, 968, 224, 180, 245, new Color(.09f, .13f, .13f));
            Box("Left button", mouse.transform, 8, 8, 78, 92, new Color(.30f, .25f, .15f)); Box("Right button", mouse.transform, 94, 8, 78, 92, new Color(.30f, .25f, .15f));
            Label("LMB", mouse.transform, "LMB\nFIRE", 8, 20, 78, 70, 18, Amber, monoFont, TextAlignmentOptions.Center);
            Label("RMB", mouse.transform, "RMB\nAIM", 94, 20, 78, 70, 18, Amber, monoFont, TextAlignmentOptions.Center);
            Box("Wheel", mouse.transform, 85, 17, 10, 38, Muted); Label("Look", mouse.transform, "MOUSE\nLOOK / AIM", 8, 125, 164, 90, 20, Ivory, monoFont, TextAlignmentOptions.Center);
            var arrows = At("Arrow keys", panel, 968, 477, 180, 74);
            string[] arrowNames = { "UP", "LEFT", "DOWN", "RIGHT" };
            for (int i = 0; i < arrowNames.Length; i++)
            {
                float x = i == 0 ? 61 : (i - 1) * 61, y = i == 0 ? 0 : 38;
                var key = Box(arrowNames[i], arrows, x, y, 57, 34, new Color(.28f, .23f, .14f));
                Label("Key label", key.transform, arrowNames[i], 0, 0, 57, 34, 13, Amber, monoFont, TextAlignmentOptions.Center);
            }
            string[] keys = { "WASD / ARROWS", "L-SHIFT", "L-CTRL / C", "SPACE", "LMB / RMB", "R", "1 / 2 / Q", "M", "ESC" };
            string[] actions = { "Move", "Sprint forward", "Crouch", "Jump", "Fire / aim down sights", "Reload", "Select / swap weapon", "Expand the minimap", "Pause / close menu" };
            for (int i = 0; i < keys.Length; i++)
            {
                float x = 40 + i % 3 * 390, y = 565 + i / 3 * 48;
                Label("Binding " + keys[i], panel, keys[i], x, y, 170, 35, 17, Amber, monoFont);
                Label("Action " + keys[i], panel, actions[i], x + 174, y - 1, 210, 40, 22, Ivory);
            }
            Label("Debug hint", panel, "F8 toggles AI diagnostics. F1-F4 are available while debug is on.", 40, 727, 1140, 30, 16, Muted, monoFont);
        }
        void KeyRow(Transform parent, float y, string row, float width)
        {
            float x = 0;
            foreach (string key in row.Split(' '))
            {
                float w = key == "SPACE" ? 320 : key == "LSHIFT" || key == "RSHIFT" ? 94 : key == "ENTER" || key == "BKSP" ? 82 : key == "TAB" || key == "CAPS" || key == "LCTRL" || key == "RCTRL" ? 76 : width;
                bool bound = " ESC 1 2 Q W A S D LSHIFT LCTRL C SPACE R M F8 ".Contains(" " + key + " ");
                var box = Box("Key " + key, parent, x, y, w - 4, 42, bound ? new Color(.28f, .23f, .14f) : new Color(.10f, .14f, .14f));
                Box("Key edge", box.transform, 0, 40, w - 4, 2, bound ? Amber : new Color(.23f, .29f, .27f));
                Label("Key label", box.transform, key, 0, 0, w - 4, 40, key.Length > 3 ? 15 : 18, bound ? Amber : Muted, monoFont, TextAlignmentOptions.Center); x += w;
            }
        }
        void BuildManual()
        {
            var panel = PagePanel("Field manual", "FIELD MANUAL", "SURVIVE. ADAPT. HOLD.", "You are the last defender of an industrial outpost. Keep its power alive."); manual = panel.gameObject;
            Button("BRIEFING", panel, "BRIEFING", 40, 195, 350, 46, () => { briefing.SetActive(true); dossier.SetActive(false); });
            Button("HOSTILES", panel, "HOSTILES", 420, 195, 350, 46, () => OpenDossier(false));
            Button("ARSENAL", panel, "ARSENAL", 800, 195, 384, 46, () => OpenDossier(true));
            briefing = At("Briefing", panel, 0, 48, 1224, 728).gameObject;
            string[] titles = { "PROTECT THE STATION", "KEEP MOVING", "PREPARE FOR HEAVIES" };
            string[] copy = {
                "Keep yourself and the power station alive. Zombies can target either one.\n\nWatch the station's health and intercept attackers before they reach the generator.",
                "Use cover, switch lanes and check the minimap. Yellow contacts are zombies; red contacts are bosses.\n\nManage your ammo. Pick up supplies and reload between engagements.",
                "Waves advance when cleared or when the timer expires. Unfinished waves can overlap.\n\nThe final assault brings bosses. Eliminate every remaining hostile to secure the outpost."
            };
            for (int i = 0; i < 3; i++)
            {
                var card = Box("Briefing " + (i + 1), briefing.transform, 40 + i * 390, 220, 366, 370, new Color(.075f, .105f, .105f));
                Label("Step", card.transform, "0" + (i + 1) + " / FIELD ORDERS", 24, 24, 318, 28, 16, Amber, monoFont);
                Label("Title", card.transform, titles[i], 24, 70, 318, 85, 33, Ivory, headingFont); Label("Body", card.transform, copy[i], 24, 160, 318, 200, 23, new Color(.74f, .77f, .71f));
            }
            Box("Manual rule", briefing.transform, 40, 600, 1144, 1, new Color(.6f, .65f, .6f, .2f));
            Label("Tactical advice", briefing.transform, "A clear lane buys time. A full magazine buys options.", 40, 625, 1144, 45, 30, Ivory, headingFont);
            Label("Manual footer", briefing.transform, "STAY AWARE. WATCH THE FLANKS. MAKE EVERY SHOT COUNT.", 40, 680, 1144, 30, 16, Amber, monoFont);
            dossier = At("Dossier", panel, 0, 260, 1224, 500).gameObject;
            var image = At("360 inspection", dossier.transform, 40, 0, 550, 440).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = true; manualViewer = image.gameObject.AddComponent<FieldManualViewer>();
            Label("Inspect hint", dossier.transform, "DRAG TO ROTATE 360 / IDLE AUTO ROTATE", 40, 456, 550, 28, 14, Amber, monoFont, TextAlignmentOptions.Center);
            Button("PREVIOUS ENTRY", dossier.transform, "< PREV", 40, 0, 140, 44, () => ChangeDossier(-1));
            Button("NEXT ENTRY", dossier.transform, "NEXT >", 450, 0, 140, 44, () => ChangeDossier(1));
            dossierCounter = Label("Entry counter", dossier.transform, "", 190, 9, 250, 28, 15, Amber, monoFont, TextAlignmentOptions.Center);
            dossierName = Label("Entry name", dossier.transform, "", 630, 0, 550, 53, 38, Ivory, headingFont);
            dossierStatus = Label("Entry status", dossier.transform, "", 630, 58, 550, 45, 16, Amber, monoFont);
            dossierDescription = Label("Entry description", dossier.transform, "", 630, 108, 550, 90, 22, Ivory);
            dossierStats = Label("Entry stats", dossier.transform, "", 630, 213, 550, 245, 18, Ivory, monoFont);
            Label("Stats note", dossier.transform, "Live loadout values. Headshots use the target's multiplier.", 630, 466, 550, 28, 17, Muted);
            dossier.SetActive(false);
        }
        void OpenDossier(bool weapons) { showWeapons = weapons; dossierIndex = 0; briefing.SetActive(false); dossier.SetActive(true); ChangeDossier(0); }
        void ChangeDossier(int delta)
        {
            var entries = System.Array.FindAll(manualEntries ?? System.Array.Empty<ManualEntry>(), e => e != null && e.visual != null && (e.weapon != null) == showWeapons);
            if (entries.Length == 0) { dossierName.text = "NO INTEL AVAILABLE"; return; }
            dossierIndex = (dossierIndex + delta + entries.Length) % entries.Length;
            var entry = entries[dossierIndex]; manualViewer.Show(entry);
            dossierName.text = entry.Title.ToUpperInvariant(); dossierDescription.text = entry.description; dossierStats.text = entry.Stats;
            dossierStatus.text = entry.weapon != null ? "STANDARD LOADOUT / " + (entry.weapon.kind == WeaponKind.Rifle ? "PRIMARY" : "SECONDARY") :
                entry.inCurrentWaves ? entry.enemy.kind == EnemyKind.Boss ? "ACTIVE BOSS / FINAL WAVE / ONE PER GATE" : "ACTIVE HOSTILE / WAVES 1-3" : "RESERVE VARIANT / NOT IN CURRENT WAVES";
            dossierCounter.text = (dossierIndex + 1) + " / " + entries.Length;
        }
    }
}
