# Popup System Design — FlowBlast

**Date:** 2026-07-14
**Status:** Approved
**Project:** FlowBlast (Unity 6.0.3, URP, namespace `FlowBlast.*`)

---

## 1. Goal

Build a **reusable popup/UI management system** for FlowBlast that:

1. Has a single entry point (`PopupManager`) so any gameplay code can request a popup in one line.
2. Inherits all new popup types from a single `BasePopup`, so adding a popup is "new class + prefab + registry entry" — no surgery on existing code.
3. Ships 3 working MVP popups: `PausePopup`, `WinPopup`, `LosePopup`.
4. Uses Unity's standard `SceneManager.LoadScene(GetActiveScene().buildIndex)` for Replay.
5. PausePopup does **not** touch `Time.timeScale` — gameplay decides its own pause; PopupManager only blocks input and stacks popups.

---

## 2. Non-Goals (YAGNI)

- No fancy animations beyond simple CanvasGroup fade.
- No localization / theme / sound.
- No pause-state machine — gameplay code checks `PopupManager.Instance.IsAnyPopupOpen` if it cares.
- No Save/Load integration this iteration.
- No new "Home" scene — Home button is a `Debug.Log` stub for now.

---

## 3. Architecture

### 3.1 High-level diagram

```
        ┌──────────────────────────────────────────────────────┐
        │                  PopupManager (singleton)            │
        │   DontDestroyOnLoad, holds Stack<BasePopup> +        │
        │   Inspector registry [PopupId → Prefab]              │
        └──────────────┬───────────────────────────────────────┘
                       │ Show<PausePopup>()  / ShowWinPopup()
                       │ Show<LosePopup>()  / CloseTopPopup()
                       ▼
        ┌──────────────────────────────────────────────────────┐
        │  Stack<BasePopup>   ← push on Show, pop on Close    │
        │  └─ top of stack = currently visible                │
        └──────────────┬───────────────────────────────────────┘
                       │ owns/instantiates
                       ▼
        ┌──────────────────────────────────────────────────────┐
        │   BasePopup (abstract MonoBehaviour)                │
        │   ├─ PausePopup   (Replay / Continue / Home)        │
        │   ├─ WinPopup     (NextLevel)                       │
        │   └─ LosePopup    (Replay)                          │
        └──────────────────────────────────────────────────────┘
```

### 3.2 Flow: Show a popup

1. Caller: `PopupManager.Instance.ShowPausePopup()` (or generic `Show<PausePopup>()`).
2. `PopupManager` looks up prefab in `_registry[PopupId.Pause]` (Inspector-configurable).
3. `Instantiate(prefab, _canvasRoot)` — parent under a dedicated `PopupCanvas`.
4. `popup.OnShown()` → enable GameObject, play fade-in.
5. Push onto `_stack`. `IsAnyPopupOpen` becomes `true`.

### 3.3 Flow: Close a popup

1. Inside popup, call `PopupManager.Instance.CloseTopPopup()` (or `this.Close()` which calls the manager).
2. Manager pops top, calls `popup.OnClosed()`, optionally starts a fade-out coroutine, then `Destroy(go)`.
3. If more popups remain → next popup stays visible (no auto-toggle).

---

## 4. Components

### 4.1 `PopupManager` (`Assets/Scripts/Managers/PopupManager.cs`)

| Member | Type | Notes |
|---|---|---|
| `Instance` | `static PopupManager` | Lazy singleton, `DontDestroyOnLoad` in `Awake`. |
| `_canvasRoot` | `Transform` | Drag a child `PopupCanvas` RectTransform here. |
| `_registry` | `PopupEntry[]` | `Serializable struct { PopupId id; GameObject prefab; }` — one entry per popup. |
| `IsAnyPopupOpen` | `bool` | `_stack.Count > 0` |
| `Show<T>() where T : BasePopup` | method | Look up prefab via `typeof(T)` → `PopupId` mapping table, instantiate, validate root has `T` component, else `LogError`. |
| `ShowPausePopup()` | method | Sugar for `Show<PausePopup>()`. |
| `ShowWinPopup()` | method | Sugar for `Show<WinPopup>()`. |
| `ShowLosePopup()` | method | Sugar for `Show<LosePopup>()`. |
| `CloseTopPopup()` | method | Pop + destroy. No-op if stack empty. |
| `CloseAll()` | method | Optional escape hatch. |
| `_stack` | `Stack<BasePopup>` | Runtime only, not serialized. |

**Initialization contract:** If no `PopupManager` exists in the scene at `Awake`, gameplay code can call `PopupManager.GetOrCreate()` (helper that lazily creates a GameObject + Canvas + component) — useful for the MVP because the user is wiring popups manually in Editor.

### 4.2 `BasePopup` (`Assets/Scripts/UI/Popup/BasePopup.cs`)

| Member | Type | Notes |
|---|---|---|
| `_canvasGroup` | `CanvasGroup` | SerializeField. Used for fade. |
| `_backdropButton` | `Button` | SerializeField. Optional. If set, clicking outside popup closes it. |
| `OnClosed` | `event Action` | Fires when popup finishes closing. |
| `OnShown()` | `protected virtual void` | Override hook. Default: enable GO, start fade-in (0→1 over 0.15s). |
| `OnClosed()` | `protected virtual void` | Override hook. Default: start fade-out (1→0 over 0.15s), then call `PopupManager.Instance.CloseTopPopup()`. |
| `Close()` | `public void` | Convenience — calls `OnClosed()`. |
| `_fadeDuration` | `float = 0.15f` | SerializeField. |

**Lifetime contract:** Prefabs are owned by `PopupManager`. `BasePopup.OnShown()` will `GetComponent<CanvasGroup>()` and auto-add one if missing, so prefab authors don't need to remember.

### 4.3 `PausePopup` (`Assets/Scripts/UI/Popup/PausePopup.cs`)

| Button | OnClick |
|---|---|
| Replay | `Replay()` → `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)` |
| Continue | `Continue()` → `Close()` |
| Home | `Home()` → `Debug.Log("[PausePopup] Home clicked — TODO: load home scene")` |

Inherits `BasePopup`. Does **not** call `Time.timeScale = 0` — gameplay decides.

### 4.4 `WinPopup` (`Assets/Scripts/UI/Popup/WinPopup.cs`)

| Button | OnClick |
|---|---|
| NextLevel | `NextLevel()` → `Debug.Log("[WinPopup] NextLevel clicked — TODO: load next level")` |

### 4.5 `LosePopup` (`Assets/Scripts/UI/Popup/LosePopup.cs`)

| Button | OnClick |
|---|---|
| Replay | `Replay()` → `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)` |

### 4.6 `PopupDemoTriggers` (`Assets/Scripts/UI/Popup/PopupDemoTriggers.cs`)

A temporary helper script for the MVP demo:

- Press **Space** → `PopupManager.Instance.ShowWinPopup()`.
- Press **L** → `PopupManager.Instance.ShowLosePopup()`.
- Press **Escape** → `PopupManager.Instance.ShowPausePopup()`.

This lives on a single GameObject in the scene (`PopupDemoTriggers`) and exists so the user can verify all 3 popups without having to wire gameplay triggers yet.

### 4.7 `PopupId` enum (`Assets/Scripts/UI/Popup/PopupId.cs`)

```csharp
public enum PopupId { Pause, Win, Lose }
```

Used as registry key. New popup → add a value here + a class + a prefab + a registry entry.

---

## 5. File Layout

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── ServiceLocator.cs          (optional, no-op stub for future)
│   ├── Managers/
│   │   └── PopupManager.cs
│   └── UI/
│       └── Popup/
│           ├── PopupId.cs
│           ├── BasePopup.cs
│           ├── PausePopup.cs
│           ├── WinPopup.cs
│           ├── LosePopup.cs
│           └── PopupDemoTriggers.cs
└── Prefabs/
    └── UI/
        └── Popup/
            ├── PF_PausePopup.prefab
            ├── PF_WinPopup.prefab
            └── PF_LosePopup.prefab
```

---

## 6. Unity Setup (Editor)

Detailed step-by-step will live in the implementation plan. Summary:

1. **Manager GameObject:** Create empty `PopupManager` GO in `Day03_ConveyorTest.unity`. Add `PopupManager` component. Create child `PopupCanvas` (Canvas, CanvasScaler, GraphicRaycaster, Screen Space - Overlay, sort order 100). Drag child RectTransform into `_canvasRoot`.
2. **Build 3 popup prefabs** in `Assets/Prefabs/UI/Popup/`:
   - Root: empty GO with `BasePopup`-derived script + `CanvasGroup`.
   - Children: full-screen dim `Image` (backdrop) + centered panel `Image` + TMP title + Buttons.
   - For each button, drag the root component into UnityEvent and pick the matching method (e.g. `PausePopup.Replay`).
3. **Registry:** In PopupManager inspector, add 3 entries mapping `PopupId.{Pause,Win,Lose}` to the corresponding prefabs.
4. **Demo trigger:** Add `PopupDemoTriggers` to the same manager GO.
5. **Test in Play mode:** Escape → Pause, Space → Win, L → Lose.

---

## 7. Error Handling

| Scenario | Behavior |
|---|---|
| `Show<T>()` but `T` is not in registry | `Debug.LogError`, no instantiation. |
| `CloseTopPopup()` on empty stack | No-op. |
| Popup prefab missing `CanvasGroup` | Auto-added in `BasePopup.OnShown()`. |
| Popup prefab missing root script | `Debug.LogError` with prefab name. |
| Replay called twice quickly | Two `LoadScene` calls — second wins; this is acceptable MVP behavior. |

---

## 8. Extensibility Checklist (how to add a 4th popup later)

1. Add value to `PopupId` enum.
2. Create class `MyNewPopup : BasePopup`, add Button handlers.
3. Build prefab `PF_MyNewPopup.prefab` (CanvasGroup on root, dim backdrop, panel, buttons).
4. Drag prefab into `PopupManager._registry` inspector.
5. Call `PopupManager.Instance.Show<MyNewPopup>()` (or add a sugar method).

No existing file needs modification beyond the enum.

---

## 9. Acceptance Criteria

- [ ] Project compiles without errors.
- [ ] Pressing Escape during Play shows PausePopup with 3 buttons.
- [ ] PausePopup → Replay reloads the current scene.
- [ ] PausePopup → Continue closes the popup, gameplay continues.
- [ ] PausePopup → Home logs `"[PausePopup] Home clicked — TODO: load home scene"` and closes.
- [ ] Pressing Space shows WinPopup; clicking NextLevel logs TODO.
- [ ] Pressing L shows LosePopup; clicking Replay reloads the scene.
- [ ] Only the top popup responds to input (backdrop blocks clicks below).
- [ ] Adding a new popup type requires no edit to `PopupManager.cs` source (only registry enum + entry).