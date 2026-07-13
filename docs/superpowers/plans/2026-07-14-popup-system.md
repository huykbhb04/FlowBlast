# Popup System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a reusable popup management system for FlowBlast with 3 working popups (Pause, Win, Lose) and a single `PopupManager` API.

**Architecture:** A singleton `PopupManager` owns a `Stack<BasePopup>` and an Inspector-configurable registry mapping `PopupId → Prefab`. Each popup inherits from `BasePopup` (CanvasGroup fade, virtual `OnShown`/`OnClosed` hooks). Gameplay code calls `PopupManager.Instance.ShowPausePopup()` etc. PausePopup does **not** touch `Time.timeScale`.

**Tech Stack:** Unity 6.0.3, URP, TextMeshPro, C# 9 (Unity default), namespace convention `FlowBlast.*`.

**Verification model:** This project has no Unity Test Framework package installed and no `Assets/Tests/` folder. Each task ends with a **manual Play-mode verification step** + commit. Manual verification = press the documented hotkey in `Day03_ConveyorTest.unity` and visually confirm the popup appears/clicks work. Acceptance criteria are listed in §9 of the spec (`docs/superpowers/specs/2026-07-14-popup-system-design.md`).

**Working directory:** Repo root is `D:\Sonat\FlowBlast_1\FlowBlast`. Branch: `Week-02`. All paths below are relative to repo root unless absolute.

---

## File Layout (locked-in decomposition)

| Path | Responsibility |
|---|---|
| `Assets/Scripts/UI/Popup/PopupId.cs` | Enum of popup identifiers. New popup = new enum value. |
| `Assets/Scripts/UI/Popup/BasePopup.cs` | Abstract base MonoBehaviour. CanvasGroup fade lifecycle, optional backdrop button, `Close()`. |
| `Assets/Scripts/UI/Popup/PausePopup.cs` | Replay / Continue / Home buttons. |
| `Assets/Scripts/UI/Popup/WinPopup.cs` | NextLevel button. |
| `Assets/Scripts/UI/Popup/LosePopup.cs` | Replay button. |
| `Assets/Scripts/UI/Popup/PopupDemoTriggers.cs` | Temp hotkey script: Esc/Space/L → Show*Popup. Lives on scene GO. |
| `Assets/Scripts/Managers/PopupManager.cs` | Singleton. Stack<BasePopup>, registry, Show<T>(), sugar methods, CloseTopPopup. |
| `Assets/Prefabs/UI/Popup/PF_PausePopup.prefab` | Pause popup prefab (built in Editor — see Task 7). |
| `Assets/Prefabs/UI/Popup/PF_WinPopup.prefab` | Win popup prefab (built in Editor — see Task 8). |
| `Assets/Prefabs/UI/Popup/PF_LosePopup.prefab` | Lose popup prefab (built in Editor — see Task 9). |
| `Assets/Scenes/Tests/Day03_ConveyorTest.unity` | Add PopupManager GO + PopupDemoTriggers (Task 10). |

---

## Task 1: PopupId enum

**Files:**
- Create: `Assets/Scripts/UI/Popup/PopupId.cs`

- [ ] **Step 1: Create the enum file**

Write exactly this to `Assets/Scripts/UI/Popup/PopupId.cs`:

```csharp
namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Identifiers for popups managed by <see cref="FlowBlast.Managers.PopupManager"/>.
    /// Adding a new popup = new enum value + new class deriving BasePopup + new prefab + new registry entry.
    /// </summary>
    public enum PopupId
    {
        Pause = 0,
        Win = 1,
        Lose = 2,
    }
}
```

- [ ] **Step 2: Verify Unity compiles**

Open the project in Unity Editor (Unity Hub → add project → open). Wait for asset import to finish (no red errors in Console). Expected: zero compile errors.

If errors appear: confirm file path is exactly `Assets/Scripts/UI/Popup/PopupId.cs` and namespace matches.

- [ ] **Step 3: Commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/UI/Popup/PopupId.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PopupId enum"
```

---

## Task 2: BasePopup abstract class

**Files:**
- Create: `Assets/Scripts/UI/Popup/BasePopup.cs`

- [ ] **Step 1: Create the base popup file**

Write exactly this to `Assets/Scripts/UI/Popup/BasePopup.cs`:

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Abstract base for every popup managed by <see cref="FlowBlast.Managers.PopupManager"/>.
    /// Owns the CanvasGroup fade lifecycle and an optional backdrop click-to-close button.
    /// Subclasses override <see cref="OnShown"/> / <see cref="OnClosed"/> to hook custom logic.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BasePopup : MonoBehaviour
    {
        [Header("BasePopup")]
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected Button _backdropButton;
        [SerializeField] protected float _fadeDuration = 0.15f;

        public event Action OnClosedEvent;

        private bool _isClosing;

        protected virtual void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_backdropButton != null)
            {
                _backdropButton.onClick.AddListener(Close);
            }

            // Start invisible; Show() will fade in.
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        protected virtual void OnDestroy()
        {
            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(Close);
            }
        }

        /// <summary>
        /// Called by PopupManager right after the prefab is instantiated and parented.
        /// Default: enable GameObject and fade alpha 0 → 1.
        /// </summary>
        public virtual void OnShown()
        {
            gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            StartCoroutine(FadeRoutine(0f, 1f, _fadeDuration));
        }

        /// <summary>
        /// Called by PopupManager when the popup is being closed (top of stack).
        /// Default: fade out, then notify PopupManager to pop & destroy.
        /// </summary>
        public virtual void OnClosed()
        {
            if (_isClosing) return;
            _isClosing = true;

            StartCoroutine(CloseRoutine());
        }

        /// <summary>Convenience for buttons to call directly on the popup.</summary>
        public void Close()
        {
            OnClosed();
        }

        private IEnumerator CloseRoutine()
        {
            yield return FadeRoutine(_canvasGroup.alpha, 0f, _fadeDuration);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            OnClosedEvent?.Invoke();
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
    }
}
```

Notes:
- Uses `Time.unscaledDeltaTime` so the fade still plays if the gameplay ever sets `Time.timeScale = 0` in the future (not used now, but harmless).
- `RequireComponent(typeof(CanvasGroup))` ensures the prefab has one — no runtime null risk.

- [ ] **Step 2: Verify Unity compiles**

Open/return to Unity Editor. Wait for asset import. Expected: zero compile errors in Console.

- [ ] **Step 3: Commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/UI/Popup/BasePopup.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add BasePopup abstract class"
```

---

## Task 3: PausePopup

**Files:**
- Create: `Assets/Scripts/UI/Popup/PausePopup.cs`

- [ ] **Step 1: Create PausePopup file**

Write exactly this to `Assets/Scripts/UI/Popup/PausePopup.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Pause popup: Replay (reload scene), Continue (close), Home (TODO stub).
    /// Does NOT touch Time.timeScale — gameplay decides its own pause.
    /// </summary>
    public class PausePopup : BasePopup
    {
        [SerializeField] private Button _replayButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _homeButton;

        private void Awake()
        {
            base.Awake();

            if (_replayButton != null) _replayButton.onClick.AddListener(Replay);
            if (_continueButton != null) _continueButton.onClick.AddListener(Continue);
            if (_homeButton != null) _homeButton.onClick.AddListener(Home);
        }

        private void OnDestroy()
        {
            if (_replayButton != null) _replayButton.onClick.RemoveListener(Replay);
            if (_continueButton != null) _continueButton.onClick.RemoveListener(Continue);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(Home);
        }

        public void Replay()
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }

        public void Continue()
        {
            Close();
        }

        public void Home()
        {
            Debug.Log("[PausePopup] Home clicked — TODO: load home scene");
            Close();
        }
    }
}
```

- [ ] **Step 2: Verify Unity compiles**

Reload Unity if needed. Expected: zero compile errors.

- [ ] **Step 3: Commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/UI/Popup/PausePopup.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PausePopup with Replay/Continue/Home"
```

---

## Task 4: WinPopup

**Files:**
- Create: `Assets/Scripts/UI/Popup/WinPopup.cs`

- [ ] **Step 1: Create WinPopup file**

Write exactly this to `Assets/Scripts/UI/Popup/WinPopup.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Win popup: single NextLevel button (stub for now — logs TODO).
    /// </summary>
    public class WinPopup : BasePopup
    {
        [SerializeField] private Button _nextLevelButton;

        private void Awake()
        {
            base.Awake();

            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(NextLevel);
        }

        private void OnDestroy()
        {
            if (_nextLevelButton != null) _nextLevelButton.onClick.RemoveListener(NextLevel);
        }

        public void NextLevel()
        {
            Debug.Log("[WinPopup] NextLevel clicked — TODO: load next level");
            Close();
        }
    }
}
```

- [ ] **Step 2: Verify Unity compiles**

Expected: zero compile errors.

- [ ] **Step 3: Commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/UI/Popup/WinPopup.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add WinPopup with NextLevel"
```

---

## Task 5: LosePopup

**Files:**
- Create: `Assets/Scripts/UI/Popup/LosePopup.cs`

- [ ] **Step 1: Create LosePopup file**

Write exactly this to `Assets/Scripts/UI/Popup/LosePopup.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Lose popup: Replay button reloads the current scene.
    /// </summary>
    public class LosePopup : BasePopup
    {
        [SerializeField] private Button _replayButton;

        private void Awake()
        {
            base.Awake();

            if (_replayButton != null) _replayButton.onClick.AddListener(Replay);
        }

        private void OnDestroy()
        {
            if (_replayButton != null) _replayButton.onClick.RemoveListener(Replay);
        }

        public void Replay()
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }
    }
}
```

- [ ] **Step 2: Verify Unity compiles**

Expected: zero compile errors.

- [ ] **Step 3: Commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/UI/Popup/LosePopup.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add LosePopup with Replay"
```

---

## Task 6: PopupManager singleton

**Files:**
- Create: `Assets/Scripts/Managers/PopupManager.cs`

- [ ] **Step 1: Create PopupManager file**

Write exactly this to `Assets/Scripts/Managers/PopupManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using FlowBlast.UI.Popup;
using UnityEngine;

namespace FlowBlast.Managers
{
    /// <summary>
    /// Central popup manager. Singleton, DontDestroyOnLoad.
    /// Holds a Stack&lt;BasePopup&gt; and an Inspector-configurable [PopupId → Prefab] registry.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        [Serializable]
        public struct PopupEntry
        {
            public PopupId id;
            public BasePopup prefab; // Prefab root must have the matching derived script.
        }

        public static PopupManager Instance { get; private set; }

        [Header("PopupManager")]
        [SerializeField] private Transform _canvasRoot;
        [SerializeField] private PopupEntry[] _registry;

        private readonly Stack<BasePopup> _stack = new Stack<BasePopup>();

        public bool IsAnyPopupOpen => _stack.Count > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Look up prefab by popup id. Returns null + logs error if not registered.</summary>
        private BasePopup GetPrefab(PopupId id)
        {
            for (int i = 0; i < _registry.Length; i++)
            {
                if (_registry[i].id == id) return _registry[i].prefab;
            }
            Debug.LogError($"[PopupManager] No registry entry for PopupId.{id}. Add one in the Inspector.");
            return null;
        }

        /// <summary>Generic entry — instantiate prefab whose root component is T.</summary>
        public T Show<T>() where T : BasePopup
        {
            Type target = typeof(T);
            for (int i = 0; i < _registry.Length; i++)
            {
                BasePopup prefab = _registry[i].prefab;
                if (prefab == null) continue;
                if (prefab.GetType() != target) continue;

                return (T)InstantiateAndShow(prefab, _registry[i].id);
            }

            Debug.LogError($"[PopupManager] No prefab of type {target.Name} found in registry.");
            return null;
        }

        /// <summary>Instantiate by id directly (used by the sugar methods).</summary>
        public BasePopup Show(PopupId id)
        {
            BasePopup prefab = GetPrefab(id);
            if (prefab == null) return null;
            return InstantiateAndShow(prefab, id);
        }

        public T ShowPausePopup<T>() where T : BasePopup { return Show<T>(); }
        public BasePopup ShowPausePopup() { return Show(PopupId.Pause); }
        public BasePopup ShowWinPopup() { return Show(PopupId.Win); }
        public BasePopup ShowLosePopup() { return Show(PopupId.Lose); }

        private BasePopup InstantiateAndShow(BasePopup prefab, PopupId id)
        {
            BasePopup instance = Instantiate(prefab, _canvasRoot);
            instance.name = $"{id}Popup (Runtime)";

            // Subscribe to OnClosedEvent so we pop & destroy when fade-out completes.
            instance.OnClosedEvent += () => HandlePopupClosed(instance);

            _stack.Push(instance);
            instance.OnShown();
            return instance;
        }

        private void HandlePopupClosed(BasePopup instance)
        {
            if (_stack.Count == 0) return;

            // Only pop if this is the top of the stack.
            BasePopup top = _stack.Peek();
            if (top == instance)
            {
                _stack.Pop();
                Destroy(instance.gameObject);
                return;
            }

            // Not on top: just destroy (shouldn't normally happen, but be safe).
            Destroy(instance.gameObject);
        }

        /// <summary>Close the topmost popup if any. No-op otherwise.</summary>
        public void CloseTopPopup()
        {
            if (_stack.Count == 0) return;
            BasePopup top = _stack.Peek();
            top.Close();
        }

        /// <summary>Escape hatch: close everything currently in the stack.</summary>
        public void CloseAll()
        {
            while (_stack.Count > 0)
            {
                BasePopup top = _stack.Pop();
                if (top != null) Destroy(top.gameObject);
            }
        }
    }
}
```

Notes:
- `OnClosedEvent` is fired by `BasePopup.CloseRoutine()` AFTER the fade-out, so destruction doesn't fight the fade.
- The `Show<T>()` overload uses type matching against registry entries — this means a single registry entry can serve both `Show<PausePopup>()` and `ShowPausePopup()` (which calls `Show(PopupId.Pause)`).

- [ ] **Step 2: Verify Unity compiles**

Expected: zero compile errors.

- [ ] **Step 3: Commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/Managers/PopupManager.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PopupManager singleton with stack + registry"
```

---

## Task 7: Build PF_PausePopup prefab (Unity Editor — manual)

**Files:**
- Create: `Assets/Prefabs/UI/Popup/PF_PausePopup.prefab` (and `.meta`)

This task is performed in the Unity Editor UI. Steps must be followed exactly.

- [ ] **Step 1: Create folder structure**

In Project window, right-click → `Create → Folder` → name `UI` under `Assets/Prefabs/`. Right-click `Assets/Prefabs/UI/` → `Create → Folder` → name `Popup`.

- [ ] **Step 2: Create the prefab root**

Right-click `Assets/Prefabs/UI/Popup/` → `Create → UI → Canvas - TextMeshPro` (if prompted to import TMP Essentials, click **Import TMP Essentials** and wait). When asked for a TextMeshPro font, pick **LiberationSans SDF** (default).

This creates a Canvas GameObject in the scene. Rename the Canvas to `PausePopupRoot`. With `PausePopupRoot` selected, in Inspector → click **Add Component** → search `Pause Popup` → add it. (RequireComponent will auto-add `CanvasGroup`.)

Drag `PausePopupRoot` from Hierarchy into `Assets/Prefabs/UI/Popup/` to make a prefab. Delete the original from the Hierarchy.

- [ ] **Step 3: Configure Canvas on prefab**

Double-click `PF_PausePopup` prefab to open Prefab Mode. Select root (`PausePopupRoot`).

In Inspector:
- **Canvas component**:
  - Render Mode: `Screen Space - Overlay`
  - Sort Order: `200`
- **CanvasScaler component**:
  - UI Scale Mode: `Scale With Screen Size`
  - Reference Resolution: `1920 x 1080`
  - Match: `0.5`
- **GraphicRaycaster**: leave defaults.
- **CanvasGroup** (auto-added by RequireComponent): leave defaults.

- [ ] **Step 4: Build the backdrop**

Right-click `PausePopupRoot` in Hierarchy → `Create → UI → Image`. Rename to `Backdrop`. With it selected:
- **RectTransform**: anchor preset = stretch-stretch (hold Alt/Shift, click the bottom-right preset in the Anchor Presets grid), Left/Right/Top/Bottom = 0.
- **Image component**: Color = `#000000`, Alpha = `180` (semi-transparent black). This is the dim layer behind the panel.

- [ ] **Step 5: Build the centered panel**

Right-click `PausePopupRoot` → `Create → UI → Image`. Rename to `Panel`. With it selected:
- **RectTransform**: anchor = middle-center, pivot = middle-center.
  - Width = 600, Height = 400.
- **Image component**: Color = `#FFFFFF` (white). This will be the popup body.

- [ ] **Step 6: Add title text**

Right-click `Panel` → `Create → UI → Text - TextMeshPro`. Rename to `Title`.
- **RectTransform**: anchor = top-stretch, pivot = top-center. Left = 0, Right = 0, Height = 80, Pos Y = 0.
- **TextMeshProUGUI component**:
  - Text = `PAUSED`
  - Font Size = 48
  - Alignment = middle-center
  - Color = `#000000`
  - Font Style = Bold

- [ ] **Step 7: Add 3 buttons**

For each of the 3 buttons, follow this pattern (right-click `Panel` → `Create → UI → Button - TextMeshPro`):

**Button A: Replay**
- Rename button to `ReplayButton`.
- RectTransform: anchor = middle-stretch, pivot = middle-center. Left = 60, Right = 60, Height = 70, Pos Y = 20.
- Select the nested `Text (TMP)` child → Text = `REPLAY`, Font Size = 32, Alignment = middle-center, Color = `#000000`.
- On the root `ReplayButton` Inspector → find **Button component → On Click ()** → click `+` → drag `PausePopupRoot` (the prefab root) into the object slot → select function `PausePopup → Replay ()` from the dropdown.

**Button B: Continue**
- Rename to `ContinueButton`.
- RectTransform: anchor = middle-stretch, pivot = middle-center. Left = 60, Right = 60, Height = 70, Pos Y = -65.
- Nested text: `CONTINUE`, Font Size = 32.
- On Click: drag root → `PausePopup → Continue ()`.

**Button C: Home**
- Rename to `HomeButton`.
- RectTransform: anchor = middle-stretch, pivot = middle-center. Left = 60, Right = 60, Height = 70, Pos Y = -150.
- Nested text: `HOME`, Font Size = 32.
- On Click: drag root → `PausePopup → Home ()`.

- [ ] **Step 8: Wire PausePopup component fields**

Select `PausePopupRoot`. In Inspector on the **PausePopup** component:
- `_replayButton` → drag `ReplayButton` from Hierarchy.
- `_continueButton` → drag `ContinueButton`.
- `_homeButton` → drag `HomeButton`.
- `_backdropButton` → leave empty (clicking outside does not close PausePopup for MVP — only the buttons do).
- `_fadeDuration` → 0.15

- [ ] **Step 9: Verify in scene**

Exit Prefab Mode. Drag `PF_PausePopup` prefab into `Day03_ConveyorTest.unity` scene (just to test). Press **Play**. Confirm popup is INVISIBLE at start (alpha 0 from BasePopup.Awake). Press **Stop**. Delete the prefab instance from the scene (we'll wire it properly via PopupManager in Task 10).

- [ ] **Step 10: Save the prefab + commit**

Prefab is auto-saved when you exit Prefab Mode. Then:

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Prefabs/UI/Popup/PF_PausePopup.prefab Assets/Prefabs/UI/Popup/PF_PausePopup.prefab.meta Assets/Prefabs/UI/Popup.meta Assets/Prefabs/UI.meta Assets/Prefabs.meta 2>nul
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PF_PausePopup prefab"
```

Note: the meta files may already exist; that's fine. Use `git status` first if you're unsure which files were added.

---

## Task 8: Build PF_WinPopup prefab (Unity Editor — manual)

**Files:**
- Create: `Assets/Prefabs/UI/Popup/PF_WinPopup.prefab` (and `.meta`)

- [ ] **Step 1: Create the prefab root**

Right-click `Assets/Prefabs/UI/Popup/` → `Create → UI → Canvas - TextMeshPro`. Rename to `WinPopupRoot`. Add **WinPopup** component via `Add Component` search.

Drag `WinPopupRoot` from Hierarchy to `Assets/Prefabs/UI/Popup/` to make prefab. Delete the scene instance.

- [ ] **Step 2: Configure Canvas on prefab (in Prefab Mode)**

Open `PF_WinPopup` prefab by double-clicking. Select root.

In Inspector:
- **Canvas**: Render Mode = `Screen Space - Overlay`, Sort Order = `200`.
- **CanvasScaler**: Scale With Screen Size, Reference = `1920 x 1080`, Match = `0.5`.

- [ ] **Step 3: Backdrop**

Right-click `WinPopupRoot` → `Create → UI → Image` → rename to `Backdrop`.
- RectTransform: stretch-stretch, all offsets = 0.
- Image Color: `#000000`, Alpha = `180`.

- [ ] **Step 4: Panel**

Right-click `WinPopupRoot` → `Create → UI → Image` → rename to `Panel`.
- RectTransform: anchor = middle-center, pivot = middle-center. Width = 600, Height = 350.
- Image Color: `#FFFFFF`.

- [ ] **Step 5: Title**

Right-click `Panel` → `Create → UI → Text - TextMeshPro` → rename to `Title`.
- RectTransform: anchor = top-stretch, pivot = top-center. Left = 0, Right = 0, Height = 80.
- Text = `YOU WIN!`, Font Size = 48, Alignment = middle-center, Color = `#000000`, Bold.

- [ ] **Step 6: NextLevel button**

Right-click `Panel` → `Create → UI → Button - TextMeshPro` → rename to `NextLevelButton`.
- RectTransform: anchor = middle-stretch, pivot = middle-center. Left = 60, Right = 60, Height = 70, Pos Y = -40.
- Nested TMP text: `NEXT LEVEL`, Font Size = 32, Color = `#000000`.
- Button OnClick: drag root → `WinPopup → NextLevel ()`.

- [ ] **Step 7: Wire WinPopup component fields**

Select `WinPopupRoot`. On **WinPopup** component:
- `_nextLevelButton` → drag `NextLevelButton`.
- `_backdropButton` → leave empty.
- `_fadeDuration` → 0.15.

- [ ] **Step 8: Save + commit**

Exit Prefab Mode. Commit:

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Prefabs/UI/Popup/PF_WinPopup.prefab Assets/Prefabs/UI/Popup/PF_WinPopup.prefab.meta
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PF_WinPopup prefab"
```

---

## Task 9: Build PF_LosePopup prefab (Unity Editor — manual)

**Files:**
- Create: `Assets/Prefabs/UI/Popup/PF_LosePopup.prefab` (and `.meta`)

- [ ] **Step 1: Create the prefab root**

Right-click `Assets/Prefabs/UI/Popup/` → `Create → UI → Canvas - TextMeshPro`. Rename to `LosePopupRoot`. Add **LosePopup** component via `Add Component`.

Drag `LosePopupRoot` to `Assets/Prefabs/UI/Popup/` to make prefab. Delete scene instance.

- [ ] **Step 2: Configure Canvas**

In Prefab Mode for `PF_LosePopup`. On root:
- **Canvas**: Overlay, Sort Order = `200`.
- **CanvasScaler**: 1920x1080, Match 0.5.

- [ ] **Step 3: Backdrop**

Right-click root → `UI → Image` → rename `Backdrop`. Stretch-stretch, all 0. Color `#000000` alpha 180.

- [ ] **Step 4: Panel**

Right-click root → `UI → Image` → rename `Panel`. Middle-center, 600x350. Color `#FFFFFF`.

- [ ] **Step 5: Title**

Right-click Panel → `UI → Text - TextMeshPro` → rename `Title`. Top-stretch, height 80. Text = `YOU LOSE`, Font Size = 48, Bold, black, middle-center.

- [ ] **Step 6: Replay button**

Right-click Panel → `UI → Button - TextMeshPro` → rename `ReplayButton`. Middle-stretch, Left/Right = 60, Height 70, Pos Y = -40. TMP text = `REPLAY`, Font Size 32, black. On Click: drag root → `LosePopup → Replay ()`.

- [ ] **Step 7: Wire LosePopup component**

Select root. On **LosePopup**:
- `_replayButton` → drag `ReplayButton`.
- `_backdropButton` → empty.
- `_fadeDuration` = 0.15.

- [ ] **Step 8: Save + commit**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Prefabs/UI/Popup/PF_LosePopup.prefab Assets/Prefabs/UI/Popup/PF_LosePopup.prefab.meta
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PF_LosePopup prefab"
```

---

## Task 10: PopupDemoTriggers script + scene wiring

**Files:**
- Create: `Assets/Scripts/UI/Popup/PopupDemoTriggers.cs`
- Modify: `Assets/Scenes/Tests/Day03_ConveyorTest.unity` (Editor only — manual)

- [ ] **Step 1: Create PopupDemoTriggers**

Write exactly this to `Assets/Scripts/UI/Popup/PopupDemoTriggers.cs`:

```csharp
using FlowBlast.Managers;
using UnityEngine;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Temporary hotkey helper for verifying the popup system end-to-end in Play mode.
    /// Attach to any GameObject in the scene.
    ///   Escape → ShowPausePopup
    ///   Space   → ShowWinPopup
    ///   L       → ShowLosePopup
    /// Will be removed once gameplay triggers (win/lose detection) are wired up.
    /// </summary>
    public class PopupDemoTriggers : MonoBehaviour
    {
        [SerializeField] private KeyCode _pauseKey = KeyCode.Escape;
        [SerializeField] private KeyCode _winKey = KeyCode.Space;
        [SerializeField] private KeyCode _loseKey = KeyCode.L;

        private void Update()
        {
            if (PopupManager.Instance == null) return;

            if (Input.GetKeyDown(_pauseKey))
            {
                PopupManager.Instance.ShowPausePopup();
            }
            else if (Input.GetKeyDown(_winKey))
            {
                PopupManager.Instance.ShowWinPopup();
            }
            else if (Input.GetKeyDown(_loseKey))
            {
                PopupManager.Instance.ShowLosePopup();
            }
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: zero compile errors.

- [ ] **Step 3: Commit script**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scripts/UI/Popup/PopupDemoTriggers.cs
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): add PopupDemoTriggers hotkey helper"
```

- [ ] **Step 4: Wire scene — create manager hierarchy**

Open `Assets/Scenes/Tests/Day03_ConveyorTest.unity` in Unity Editor.

In Hierarchy, create empty GameObject → rename `PopupManager`. With it selected:
- Inspector → **Add Component** → `Popup Manager` (the script from Task 6).

Create child GameObject under `PopupManager` → rename `PopupCanvas`. Select it:
- Inspector → **Add Component** → `Canvas`. (It will already have Canvas + CanvasScaler + GraphicRaycaster — that's fine.)
- Canvas Render Mode = `Screen Space - Overlay`, Sort Order = `100`.
- CanvasScaler: Scale With Screen Size, Reference 1920x1080, Match 0.5.

Drag `PopupCanvas` RectTransform into `PopupManager._canvasRoot` slot.

- [ ] **Step 5: Wire registry**

On `PopupManager` GameObject → Inspector → **Popup Manager → Registry** array → set Size = 3.

Configure 3 entries:
- Element 0: `Id = Pause`, `Prefab = PF_PausePopup`
- Element 1: `Id = Win`, `Prefab = PF_WinPopup`
- Element 2: `Id = Lose`, `Prefab = PF_LosePopup`

To drag the prefab, open `Assets/Prefabs/UI/Popup/` in Project window and drag the prefab into the slot.

- [ ] **Step 6: Add PopupDemoTriggers**

On the `PopupManager` GameObject → **Add Component** → `Popup Demo Triggers`. Leave defaults (Esc / Space / L).

- [ ] **Step 7: Save scene + commit**

`File → Save` (Ctrl+S) for `Day03_ConveyorTest.unity`.

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git add Assets/Scenes/Tests/Day03_ConveyorTest.unity Assets/Scenes/Tests/Day03_ConveyorTest.unity.meta
git -c user.email="cursor@local" -c user.name="Cursor" commit -m "feat(popup): wire PopupManager + demo triggers in Day03 scene"
```

---

## Task 11: End-to-end verification (Play mode)

This task runs all acceptance checks from spec §9. **Do not skip — this is the verification step required by the verification-before-completion skill.**

- [ ] **Step 1: Open scene + press Play**

Open `Assets/Scenes/Tests/Day03_ConveyorTest.unity`. Press **Play**.

Expected: no console errors. The `PopupCanvas` is visible (but empty — no popups).

- [ ] **Step 2: Verify Escape → PausePopup**

Press **Escape**. Expected:
- PausePopup fades in.
- Title shows "PAUSED".
- 3 buttons visible: REPLAY / CONTINUE / HOME.

Click **CONTINUE**. Expected: popup fades out and disappears. No console errors.

- [ ] **Step 3: Verify PausePopup → Replay**

Press Escape to show Pause. Click **REPLAY**. Expected: scene reloads (brief flash of black), you return to the initial frame. Console shows no errors.

- [ ] **Step 4: Verify PausePopup → Home**

Press Escape. Click **HOME**. Expected: popup fades out, console shows `[PausePopup] Home clicked — TODO: load home scene`.

- [ ] **Step 5: Verify Space → WinPopup**

Press **Space**. Expected: WinPopup fades in, shows "YOU WIN!" and NEXT LEVEL button.

Click **NEXT LEVEL**. Expected: console shows `[WinPopup] NextLevel clicked — TODO: load next level`, popup fades out.

- [ ] **Step 6: Verify L → LosePopup**

Press **L**. Expected: LosePopup fades in, shows "YOU LOSE" and REPLAY button.

Click **REPLAY**. Expected: scene reloads.

- [ ] **Step 7: Verify IsAnyPopupOpen behavior**

Press Escape → PausePopup appears. Open Hierarchy → expand `PopupCanvas` → you should see one child named `PausePopup (Runtime)`.

Press Space while Pause is still open. Expected: WinPopup appears on top of PausePopup (both visible). The PausePopup buttons should NOT respond to clicks (backdrop blocks input below the top popup).

Click NEXT LEVEL on Win. Expected: WinPopup closes, PausePopup buttons become clickable again. Click CONTINUE on Pause. Expected: Pause closes, stack empty.

- [ ] **Step 8: Stop Play + final commit (if any pending)**

Press **Stop**. If anything was modified in the scene accidentally, save and commit. Otherwise no commit needed.

- [ ] **Step 9: Final summary commit (if not already committed)**

```bash
cd "D:\Sonat\FlowBlast_1\FlowBlast"
git status
```

If there are any uncommitted changes (e.g. scene tweaks, lock file), commit them with message `chore(popup): end-to-end verification cleanup`.

---

## Self-Review (plan vs spec)

Spec coverage:
- §3.1 architecture: covered in Tasks 2, 6.
- §3.2-3.3 show/close flow: covered in Tasks 2, 6.
- §4.1 PopupManager API: covered in Task 6.
- §4.2 BasePopup: covered in Task 2.
- §4.3-4.5 the 3 popups: covered in Tasks 3, 4, 5.
- §4.6 PopupDemoTriggers: covered in Task 10.
- §4.7 PopupId: covered in Task 1.
- §5 file layout: covered.
- §6 Unity setup: covered in Tasks 7-10.
- §7 error handling: covered in Task 6 (`LogError` paths).
- §8 extensibility: implicit — adding enum value + new class + prefab follows the same task pattern.
- §9 acceptance criteria: covered in Task 11 verification.

No placeholders. No TBDs. No "similar to" without code. All file paths exact. All code blocks complete.

Type consistency check:
- `BasePopup._canvasGroup`, `_backdropButton`, `_fadeDuration`, `OnShown()`, `OnClosed()`, `Close()`, `OnClosedEvent` — used consistently in Tasks 2, 3, 4, 5, 6.
- `PopupManager.Instance`, `Show<T>()`, `Show(PopupId)`, `ShowPausePopup()`, `ShowWinPopup()`, `ShowLosePopup()`, `CloseTopPopup()`, `IsAnyPopupOpen` — used consistently in Tasks 6, 10.
- `PopupId.{Pause, Win, Lose}` — defined in Task 1, used in Task 6.

No inconsistencies found.

---

## End of Plan