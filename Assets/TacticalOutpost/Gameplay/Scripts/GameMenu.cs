using System.Collections;
using Outpost.Enemy;
using Outpost.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outpost.UI
{
    public sealed class GameMenu : MonoBehaviour
    {
        public const string MenuScene = "Assets/Scenes/Gameplay/TacticalOutpost_Menu.unity";
        public const string GameScene = "Assets/Scenes/Gameplay/TacticalOutpost_Game.unity";
        public static GameMenu Instance { get; private set; }
        public bool mainMenu;
        public Sprite primaryButton, secondaryButton, panelSprite;
        public Font font;
        public bool IsPaused { get; private set; }
        GameObject overlay;
        bool loading;

        void Awake() { Instance = this; }
        void Start()
        {
            BuildUi();
            overlay.SetActive(mainMenu);
            if (mainMenu) { Time.timeScale = 1f; AudioListener.pause = false; FpsController.LockCursor(false); }
        }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f; AudioListener.pause = false;
        }

        public void TogglePause()
        {
            if (mainMenu || loading || overlay == null) return;
            var hp = PlayerHealth.Instance;
            var wave = WaveDirector.Instance;
            if (hp == null || hp.IsDead || (wave != null && (wave.CurrentPhase == WaveDirector.Phase.Defeat || wave.CurrentPhase == WaveDirector.Phase.Victory))) return;
            if (IsPaused) { Resume(); return; }
            IsPaused = true; overlay.SetActive(true);
            hp.GetComponent<FpsController>().InputEnabled = false;
            Time.timeScale = 0f; AudioListener.pause = true;
            FpsController.LockCursor(false);
            SelectFirst();
        }

        public void Resume()
        {
            if (!IsPaused || loading) return;
            IsPaused = false; overlay.SetActive(false);
            Time.timeScale = 1f; AudioListener.pause = false;
            StartCoroutine(RestoreControls());
        }
        IEnumerator RestoreControls()
        {
            // Do not fire the weapon with the same click that selected Resume.
            yield return null;
            if (IsPaused || loading || PlayerHealth.Instance == null || PlayerHealth.Instance.IsDead) yield break;
            PlayerHealth.Instance.GetComponent<FpsController>().InputEnabled = true;
            FpsController.LockCursor(true);
        }
        public void StartGame()
        {
            if (loading) return;
            loading = true; Time.timeScale = 1f; AudioListener.pause = false;
            SceneManager.LoadScene(GameScene);
        }
        public void Exit()
        {
            if (loading) return;
            loading = true; Time.timeScale = 1f; AudioListener.pause = false;
            if (!mainMenu) { SceneManager.LoadScene(MenuScene); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SelectFirst()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(overlay.GetComponentInChildren<Button>().gameObject);
        }
        RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        Text Label(string name, Transform parent, string text, int size, Vector2 box, Vector2 position, Color color)
        {
            var rect = Rect(name, parent, box, position);var label = rect.gameObject.AddComponent<Text>();
            label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;label.fontSize = size;label.alignment = TextAnchor.MiddleCenter;label.color = color;label.raycastTarget = false;
            return label;
        }
        void MenuButton(Transform parent, string text, float y, Sprite sprite, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(text, parent, new Vector2(390,134), new Vector2(0,y));
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.color = text == "EXIT" ? new Color(.547f,.547f,.547f) : new Color(.216f,.471f,.682f);
            var button = rect.gameObject.AddComponent<Button>();button.targetGraphic = image;
            var colors = button.colors;colors.highlightedColor = new Color(1.15f,1.15f,1.15f);colors.pressedColor = new Color(.65f,.85f,.95f);colors.selectedColor = new Color(1.1f,1.1f,1.1f);button.colors = colors;
            button.onClick.AddListener(action);
            Label("Label",rect,text,30,new Vector2(390,70),Vector2.zero,new Color(.9f,.97f,1f));
        }
        void BuildUi()
        {
            if (EventSystem.current == null) new GameObject("Menu EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            overlay = new GameObject("Menu overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform,false);
            var canvas = overlay.GetComponent<Canvas>();canvas.renderMode = RenderMode.ScreenSpaceOverlay;canvas.sortingOrder = 200;
            var scaler = overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution = new Vector2(1920,1080);scaler.matchWidthOrHeight = .5f;
            var background = Rect("Backdrop",overlay.transform,Vector2.zero,Vector2.zero);
            background.anchorMin = Vector2.zero;background.anchorMax = Vector2.one;background.offsetMin = background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = new Color(.018f,.028f,.045f,mainMenu?1f:.9f);
            var panel = Rect("Menu panel",overlay.transform,new Vector2(700,730),Vector2.zero);
            var image = panel.gameObject.AddComponent<Image>();image.sprite = panelSprite;image.type = Image.Type.Sliced;image.color = panelSprite != null ? Color.white : new Color(.025f,.06f,.085f,.98f);
            Label("Heading",panel,mainMenu?"TACTICAL OUTPOST":"PAUSED",48,new Vector2(680,90),new Vector2(0,235),new Color(.88f,.96f,1f));
            Label("Subtitle",panel,mainMenu?"HOLD THE LINE. PROTECT THE OUTPOST.":"Take a breath. Get back in the fight.",22,new Vector2(650,70),new Vector2(0,155),new Color(.52f,.72f,.8f));
            MenuButton(panel,mainMenu?"START":"RESUME",-30,primaryButton,mainMenu?(UnityEngine.Events.UnityAction)StartGame:Resume);
            MenuButton(panel,"EXIT",-185,secondaryButton,Exit);
            Label("Footer",panel,mainMenu?"SOLO DEFENSE":"Exit returns to the main menu.",18,new Vector2(650,50),new Vector2(0,-305),new Color(.44f,.6f,.66f));
            if (mainMenu) SelectFirst();
        }
    }
}
