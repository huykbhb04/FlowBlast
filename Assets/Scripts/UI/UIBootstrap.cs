using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.UI
{
    /// <summary>
    /// Auto-builds a Canvas + Scale With Screen Size + EventSystem + all UI panels
    /// (MainMenu, HUD, Pause, Win, Lose) if none exist in the scene. Lets the project
    /// run a complete UI flow even when the .unity file has no manually-set-up Canvas,
    /// mirroring the ConveyorAutoBootstrap pattern.
    ///
    /// Drop a single instance of this on an empty GameObject in any scene that should have
    /// the full UI set (the gameplay scene Day03_ConveyorTest, for example). Set
    /// <see cref="_stateAtRuntime"/> to choose which screen should be visible at start:
    ///   Playing  -> HUD only (gameplay scene)
    ///   MainMenu -> menu scene
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class UIBootstrap : MonoBehaviour
    {
        public enum InitialUIState { MainMenu, Playing }

        [Header("Auto-build")]
        [SerializeField] private bool _autoBuildIfMissing = true;
        [SerializeField] private InitialUIState _stateAtRuntime = InitialUIState.Playing;

        [Header("Reference (1920x1080 landscape reference)")]
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920, 1080);
        [SerializeField] private float _matchWidthOrHeight = 0.5f;
        [SerializeField] private int _coinRewardOnWin = 25;

        [Header("Theme (300Mind)")]
        [Tooltip("Optional. When assigned, controllers rebuild their hierarchy via UIThemeBuilder.")]
        [SerializeField] private UITheme_300Mind _theme;

        private static bool _builtThisDomain;

        private void Awake()
        {
            // UIManager MUST exist before panels subscribe to OnStateChanged in their OnEnable.
            // So we ensure it first, then build panels.
            EnsureUIManager();
            EnsureBuilt(createCanvasIfMissing: !FindObjectOfType<Canvas>());
            EnsureWinListener();
            EnsureDebugOverlay();

            // After panels exist (built or hand-authored), apply the 300Mind theme if any.
            ApplyTheme();
        }

        private void Start()
        {
            // After everything is built, push the configured initial state to UIManager so
            // the right panel lights up.
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetState(
                    _stateAtRuntime == InitialUIState.MainMenu ? UIState.MainMenu : UIState.Playing);
            }
        }

        private static void EnsureDebugOverlay()
        {
            if (FindObjectOfType<MonoBehaviour>() != null &&
                System.Array.Find(FindObjectsOfType<MonoBehaviour>(),
                    x => x != null && x.GetType().FullName == "FlowBlast.Diagnostics.SceneDebugOverlay") != null)
                return;

            var go = new GameObject("__FlowBlast_SceneDebugOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent(System.Type.GetType(
                "FlowBlast.Diagnostics.SceneDebugOverlay, Assembly-CSharp"));
        }

        private void EnsureUIManager()
        {
            if (UIManager.Instance != null) return;
            var go = new GameObject("__FlowBlast_UI_Manager");
            DontDestroyOnLoad(go);
            var mgr = go.AddComponent<UIManager>();

            // Pick initial state based on bootstrap mode.
            var initialField = typeof(UIManager).GetField("_initialState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initialField?.SetValue(mgr, _stateAtRuntime == InitialUIState.MainMenu ? UIState.MainMenu : UIState.Playing);
        }

        private void EnsureWinListener()
        {
            if (FindObjectOfType<ProgressWinListener>() != null) return;
            var go = new GameObject("__FlowBlast_Win_Listener");
            DontDestroyOnLoad(go);
            go.AddComponent<ProgressWinListener>();
        }

        // ----------------------------------------------------------------------

        private void EnsureBuilt(bool createCanvasIfMissing = true)
        {
            // Canvas root
            GameObject canvasGo;
            if (createCanvasIfMissing)
            {
                canvasGo = new GameObject("__FlowBlast_AutoCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = _referenceResolution;
                scaler.matchWidthOrHeight = _matchWidthOrHeight;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

                canvasGo.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvasGo = FindObjectOfType<Canvas>().gameObject;
            }

            // EventSystem (so buttons work in Editor + standalone)
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("__FlowBlast_EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ----- Panel roots -----
            var pausePanel = MakePanel(canvasGo.transform, "PausePanel", out GameObject pauseRoot,
                out RectTransform pauseRect, fullScreen: true, tint: new Color(0f, 0f, 0f, 0.55f));
            var winPanel   = MakePanel(canvasGo.transform, "WinPanel", out GameObject winRoot,
                out RectTransform winRect, fullScreen: true, tint: new Color(0f, 0f, 0f, 0.55f));
            var losePanel  = MakePanel(canvasGo.transform, "LosePanel", out GameObject loseRoot,
                out RectTransform loseRect, fullScreen: true, tint: new Color(0f, 0f, 0f, 0.55f));
            var menuPanel  = MakePanel(canvasGo.transform, "MainMenuPanel", out GameObject menuRoot,
                out RectTransform menuRect, fullScreen: true, tint: new Color(0.08f, 0.12f, 0.2f, 1f));
            var hudPanel   = MakePanel(canvasGo.transform, "HUDPanel", out GameObject hudRoot,
                out RectTransform hudRect, fullScreen: false, tint: null);

            // ----- Attach controllers -----
            AssignState(menuPanel,  UIState.MainMenu);
            AssignState(hudPanel,   UIState.Playing);
            AssignState(pausePanel, UIState.Paused);
            AssignState(winPanel,   UIState.Won);
            AssignState(losePanel,  UIState.Lost);

            // ----- Children (buttons + labels) -----
            BuildMainMenuChildren(menuPanel);
            BuildHUDChildren(hudPanel);
            BuildPauseChildren(pausePanel);
            BuildWinChildren(winPanel, isWin: true);
            BuildLoseChildren(losePanel);
            BuildSafeArea(canvasGo.transform);

            // If we boot straight into Playing, immediately hide the MainMenu panel
            // so it doesn't cover the scene with its dark tint.
            if (_stateAtRuntime == InitialUIState.Playing)
            {
                menuPanel.SetActive(false);
                hudPanel.SetActive(true);
                pausePanel.SetActive(false);
                winPanel.SetActive(false);
                losePanel.SetActive(false);
            }
            else
            {
                menuPanel.SetActive(true);
                hudPanel.SetActive(false);
                pausePanel.SetActive(false);
                winPanel.SetActive(false);
                losePanel.SetActive(false);
            }

            _builtThisDomain = true;
            Debug.Log($"[UIBootstrap] Auto-built UI Canvas + 5 panels in mode={_stateAtRuntime}.");
        }

        // -------- panel factory --------
        private GameObject MakePanel(Transform parent, string name,
            out GameObject rootGo, out RectTransform rt, bool fullScreen, Color? tint)
        {
            rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);
            rt = rootGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (!fullScreen)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 240f);
                rt.anchoredPosition = Vector2.zero;
            }

            if (tint.HasValue)
            {
                var img = rootGo.AddComponent<Image>();
                img.color = tint.Value;
                img.raycastTarget = true;
            }

            return rootGo;
        }

        private void AssignState(GameObject panel, UIState state)
        {
            // Use either MainMenuController, HUDController, PauseController or WinLoseController
            // depending on the target state. We always attach a UIPanel-derived component so it
            // has the same enable/disable plumbing.
            switch (state)
            {
                case UIState.MainMenu: panel.AddComponent<MainMenuController>(); break;
                case UIState.Playing:  panel.AddComponent<HUDController>(); break;
                case UIState.Paused:   panel.AddComponent<PauseController>(); break;
                case UIState.Won:
                case UIState.Lost:
                    var wlc = panel.AddComponent<WinLoseController>();
                    var f = typeof(WinLoseController).GetField("_shownStates",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.FlattenHierarchy);
                    f?.SetValue(wlc, new[] { state });
                    break;
            }
        }

        // -------- UI builder helpers --------
        private static GameObject NewGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Stretch(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 size, Vector2 pos, Color? color = null)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            if (color.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = color.Value;
            }
            return rt;
        }

        private static Button MakeButton(Transform parent, string label, Vector2 anchor,
            out RectTransform rt, Color color)
        {
            var go = NewGO($"{label}_Button", parent);
            rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(360f, 110f);
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var txtGo = NewGO("Label", go.transform);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 48;
            t.color = Color.white;

            return btn;
        }

        private static TextMeshProUGUI MakeTMP(Transform parent, string name, string text,
            Vector2 anchor, Vector2 size, int fontSize, Color color, TextAlignmentOptions align)
        {
            var go = NewGO(name, parent);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            return t;
        }

        private static void WireButton(Button btn, System.Reflection.FieldInfo[] fields, params object[] inject)
        {
            // No-op placeholder - controllers don't expose UnityEvents publicly, so we wire
            // through Inspector when set up by hand. Auto-built panels use this via private fields.
        }

        // ---- Build HUD ----
        private void BuildHUDChildren(GameObject panel)
        {
            var hud = panel.GetComponent<HUDController>();
            var fields = typeof(HUDController)
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Progress label: top-left
            var progress = MakeTMP(panel.transform, "ProgressLabel", "Boxes: 0/0",
                new Vector2(0f, 1f), new Vector2(500, 80), 48, Color.white,
                TextAlignmentOptions.TopLeft);
            BindByName(hud, fields, "_progressLabel", progress);

            // Coin label: top-right
            var coin = MakeTMP(panel.transform, "CoinLabel", "0",
                new Vector2(1f, 1f), new Vector2(300, 80), 48, Color.yellow,
                TextAlignmentOptions.TopRight);
            BindByName(hud, fields, "_coinLabel", coin);

            // Level label: top-center
            var lvl = MakeTMP(panel.transform, "LevelLabel", "Level 1",
                new Vector2(0.5f, 1f), new Vector2(400, 80), 48, Color.white,
                TextAlignmentOptions.Top);
            BindByName(hud, fields, "_levelLabel", lvl);

            // Pause button: bottom-left or top-center
            var pauseBtn = MakeButton(panel.transform, "II",
                new Vector2(0f, 1f), out RectTransform pauseRt, new Color(0f, 0f, 0f, 0.6f));
            pauseRt.anchorMin = pauseRt.anchorMax = new Vector2(0f, 1f);
            pauseRt.pivot = new Vector2(0f, 1f);
            pauseRt.sizeDelta = new Vector2(80, 80);
            pauseRt.anchoredPosition = new Vector2(20, -20);
            BindByName(hud, fields, "_pauseButton", pauseBtn);
        }

        private void BuildMainMenuChildren(GameObject panel)
        {
            var ctrl = panel.GetComponent<MainMenuController>();
            var fields = typeof(MainMenuController)
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var title = MakeTMP(panel.transform, "Title", "FlowBlast",
                new Vector2(0.5f, 0.7f), new Vector2(900, 200), 96, Color.white,
                TextAlignmentOptions.Center);

            var coinLbl = MakeTMP(panel.transform, "Coins", "0",
                new Vector2(0.95f, 0.95f), new Vector2(300, 60), 36, Color.yellow,
                TextAlignmentOptions.TopRight);
            BindByName(ctrl, fields, "_coinsText", coinLbl);

            var playBtn = MakeButton(panel.transform, "Play",
                new Vector2(0.5f, 0.4f), out RectTransform playRt, new Color(0.2f, 0.6f, 1f, 1f));
            BindByName(ctrl, fields, "_playButton", playBtn);

            var quitBtn = MakeButton(panel.transform, "Quit",
                new Vector2(0.5f, 0.25f), out RectTransform quitRt, new Color(0.6f, 0.2f, 0.2f, 1f));
            BindByName(ctrl, fields, "_quitButton", quitBtn);
        }

        private void BuildPauseChildren(GameObject panel)
        {
            var ctrl = panel.GetComponent<PauseController>();
            var fields = typeof(PauseController)
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            MakeTMP(panel.transform, "PauseTitle", "Paused",
                new Vector2(0.5f, 0.7f), new Vector2(900, 200), 80, Color.white,
                TextAlignmentOptions.Center);

            var resume = MakeButton(panel.transform, "Resume",
                new Vector2(0.5f, 0.5f), out _, new Color(0.2f, 0.8f, 0.3f, 1f));
            BindByName(ctrl, fields, "_resumeButton", resume);

            var restart = MakeButton(panel.transform, "Restart",
                new Vector2(0.5f, 0.4f), out _, new Color(0.3f, 0.5f, 1f, 1f));
            BindByName(ctrl, fields, "_restartButton", restart);

            var menu = MakeButton(panel.transform, "Main Menu",
                new Vector2(0.5f, 0.3f), out _, new Color(0.6f, 0.6f, 0.6f, 1f));
            BindByName(ctrl, fields, "_mainMenuButton", menu);
        }

        private void BuildWinChildren(GameObject panel, bool isWin)
        {
            var ctrl = panel.GetComponent<WinLoseController>();
            var fields = typeof(WinLoseController)
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            MakeTMP(panel.transform, "WinTitle", "Level Cleared!",
                new Vector2(0.5f, 0.7f), new Vector2(900, 200), 80, Color.green,
                TextAlignmentOptions.Center);

            var reward = MakeTMP(panel.transform, "Reward", "+0",
                new Vector2(0.5f, 0.6f), new Vector2(400, 80), 60, Color.yellow,
                TextAlignmentOptions.Center);
            BindByName(ctrl, fields, "_rewardText", reward);

            var next = MakeButton(panel.transform, isWin ? "Next" : "Restart",
                new Vector2(0.5f, 0.4f), out _, new Color(0.2f, 0.8f, 0.3f, 1f));
            BindByName(ctrl, fields, isWin ? "_nextButton" : "_restartButton", next);

            var menu = MakeButton(panel.transform, "Main Menu",
                new Vector2(0.5f, 0.3f), out _, new Color(0.6f, 0.6f, 0.6f, 1f));
            BindByName(ctrl, fields, "_mainMenuButton", menu);

            if (isWin)
            {
                var f = typeof(WinLoseController).GetField("_coinsReward",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(ctrl, _coinRewardOnWin);
            }
            else
            {
                var f = typeof(WinLoseController).GetField("_coinsReward",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(ctrl, 0);
            }
        }

        private void BuildLoseChildren(GameObject panel)
        {
            BuildWinChildren(panel, isWin: false);
            // Replace win title with a lose one.
            var t = panel.transform.Find("WinTitle");
            if (t != null)
            {
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "Try Again";
                    tmp.color = new Color(1f, 0.4f, 0.4f);
                }
            }
        }

        private void BuildSafeArea(Transform canvasParent)
        {
            var go = NewGO("HUD_SafeArea", canvasParent);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<UISafeArea>();
            // Re-parent HUD to the safe-area root so HUD elements respect the notch.
            var hud = canvasParent.Find("HUDPanel");
            if (hud != null) hud.SetParent(go.transform, false);
        }

        private static void BindByName(MonoBehaviour owner, System.Reflection.FieldInfo[] fields,
            string fieldName, object value)
        {
            var f = System.Array.Find(fields, x => x.Name == fieldName);
            if (f != null) f.SetValue(owner, value);
        }

        /// <summary>
        /// Walks the scene, finds any controller-derived component that hasn't had
        /// <see cref="UIPanel.BuildHierarchy"/> called yet, and rebuilds it from
        /// <see cref="_theme"/>. Controllers rebuilt by <c>EnsureBuilt</c> already have
        /// their children wired and don't need a second pass — we detect that by looking
        /// for our "Title" / "ProgressLabel" sentinel child names.
        ///
        /// Also creates the optional LevelSelect / Settings panels if the controller
        /// exists in the scene (e.g. via menu installers) but isn't present in the
        /// standard 5-panel set.
        /// </summary>
        private void ApplyTheme()
        {
            if (_theme == null) return;

            // Apply to every UIPanel-derived controller that the bootstrap or hand-authored
            // scene has produced.
            var controllers = Object.FindObjectsOfType<UIPanel>();
            foreach (var ctrl in controllers)
            {
                if (ctrl == null) continue;
                if (HasThemeSentinel(ctrl.transform)) continue;
                ctrl.BuildHierarchy(_theme);
            }

            // Also ensure LevelSelect / Settings panels exist if any controller in the scene
            // requires them (they are not part of the default 5-panel set).
            EnsureOptionalPanel<LevelSelectController>(UIState.LevelSelect);
            EnsureOptionalPanel<SettingsController>(UIState.Settings);
        }

        private static bool HasThemeSentinel(Transform t)
        {
            // Any of our controllers inject one of these named children when BuildHierarchy
            // runs. If present we assume theme already applied.
            if (t == null) return false;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == "Background" || c.name == "PausePanelInner"
                    || c.name == "ResultPanel" || c.name == "ScrollView")
                    return true;
            }
            return false;
        }

        private void EnsureOptionalPanel<T>(UIState state) where T : UIPanel
        {
            if (Object.FindObjectOfType<T>() != null) return;
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject(typeof(T).Name.Replace("Controller", "Panel"));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;

            var ctrl = go.AddComponent<T>();
            var f = typeof(UIPanel).GetField("_shownStates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.FlattenHierarchy);
            f?.SetValue(ctrl, new[] { state });

            if (_theme != null) ctrl.BuildHierarchy(_theme);
            go.SetActive(false);
        }
    }
}
