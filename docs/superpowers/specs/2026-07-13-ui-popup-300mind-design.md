# UI Pop-up — 300Mind Design Spec

**Date:** 2026-07-13
**Status:** Approved (brainstorm complete)
**Owner:** Unity UI

## 1. Purpose

Replace the existing placeholder UI (gray panels, default Unity buttons, Oswald fallback) with a polished pop-up UI built from the imported **300Mind / 2D Game UI Kit** asset pack. The UI must cover a full game flow (MainMenu → LevelSelect → Playing → Paused → Won) and live entirely in code via auto-bootstrap so the project runs with one-click from either scene.

## 2. Scope

**In scope:**
- 7 panels: MainMenu, LevelSelect, Settings, HUD, Pause, Win, Lose.
- 2 scenes: new `Assets/Scenes/MainMenu.unity`, existing `Assets/Scenes/Tests/Day03_ConveyorTest.unity`.
- Auto-build UI on Play with one `UIBootstrap` component per scene.
- Persisted state (coins, unlocked level, settings) via PlayerPrefs.
- Mobile portrait safe area handling.

**Out of scope:**
- Animation polish (DOTween/Animator) — explicit in DAY06 guide as future work.
- Custom Lose trigger from gameplay (layout ready, no logic).
- Multi-level flow beyond the existing single demo level.
- Sound / music implementation (toggle UI ready).
- Localization.

## 3. Architecture

### 3.1 Layer overview

```
┌────────────────────────────────────────────┐
│ UIManager (state machine + scene loader)   │
├────────────────────────────────────────────┤
│ UIBootstrap (auto-build entry point)       │
│   ├─ loads UITheme_300Mind                 │
│   ├─ calls UIThemeBuilder.ApplyTo(panel)   │
│   └─ initial state = MainMenu | Playing    │
├────────────────────────────────────────────┤
│ UIThemeBuilder (static helpers)            │
│   BuildPanel / BuildButton / BuildText     │
│   BuildIcon / BuildProgressBar / BuildToggle│
├────────────────────────────────────────────┤
│ UITheme_300Mind (ScriptableObject)         │
│   sprites + fonts + colors                 │
├────────────────────────────────────────────┤
│ Controllers (per-panel)                    │
│   MainMenu / LevelSelect / Settings        │
│   HUD / Pause / WinLose / Popup            │
└────────────────────────────────────────────┘
```

### 3.2 Theme asset

`UITheme_300Mind` ScriptableObject fields:

| Field | Type | Source |
|---|---|---|
| `panelBackground` | Sprite (9-sliced rounded rect) | `UI-pack_Sprite_1.png` |
| `panelHeader` | Sprite | same |
| `buttonNormal` | Sprite (9-sliced) | same |
| `buttonPressed` | Sprite | same |
| `buttonDisabled` | Sprite | same |
| `progressBarBg` | Sprite | same |
| `progressBarFill` | Sprite | same |
| `iconCoin` | Sprite | `UI-pack_Sprite_2.png` |
| `iconStar` | Sprite | same |
| `iconSettings` | Sprite | same |
| `iconLevel` | Sprite | same |
| `iconPlay` | Sprite | same |
| `iconQuit` | Sprite | same |
| `iconBack` | Sprite | same |
| `iconPause` | Sprite | same |
| `iconRestart` | Sprite | same |
| `iconMusic`, `iconSfx`, `iconVibration` | Sprite | same |
| `titleFont` | TMP_FontAsset | Oswald-Bold TTF (imported) |
| `bodyFont` | TMP_FontAsset | Oswald-Regular TTF |
| `buttonFont` | TMP_FontAsset | Oswald-SemiBold TTF |
| `palettePrimary` | Color | orange/yellow accent |
| `paletteAccent` | Color | teal/green accent |
| `paletteText` | Color | dark brown for body text |
| `paletteTitle` | Color | white with shadow |

Asset file path: `Assets/UI/UITheme_300Mind.asset`. Created via custom inspector menu `FlowBlast/UI/Create 300Mind Theme`.

### 3.3 Builder API

`UIThemeBuilder` (static class) — receives `UITheme_300Mind` + `Transform parent`:

| Method | Returns | Purpose |
|---|---|---|
| `BuildPanel(parent, name, anchorMode, theme)` | GameObject | Rounded-rect container with optional header |
| `BuildButton(parent, label, theme, onClick)` | Button | 9-sliced sprite + TMP text + SFX hook |
| `BuildIconButton(parent, sprite, theme, onClick)` | Button | Icon-only square button |
| `BuildText(parent, font, size, color, align)` | TMP_Text | Label without background |
| `BuildIcon(parent, sprite, size, theme)` | Image | Decorative icon |
| `BuildProgressBar(parent, theme)` | (Image bg, Image fill) | Reusable bar component |
| `BuildToggle(parent, label, theme, initial)` | Toggle | On/off switch with icon |
| `BuildBackdrop(parent, theme, alpha)` | Image | Full-screen dark overlay |

All builders attach `RectTransform` with anchor presets matching the design (center, stretch, top, etc.) so panels auto-adapt to screen size.

### 3.4 Panel controllers

Each controller inherits the existing `UIPanel` base. New behavior:

| Controller | Existing fields | New `BuildHierarchy(UITheme theme)` responsibility |
|---|---|---|
| `MainMenuController` | Play/Levels/Settings/Quit buttons | Inject 4 buttons + title + bottom coin label |
| `HUDController` | ProgressLabel/CoinLabel/PauseButton | Top-bar layout: pause left, coin right, progress center, level bottom |
| `PauseController` | Resume/Restart/Menu buttons | Overlay + centered panel with 3 buttons |
| `WinLoseController` | Next/Restart/Menu buttons + reward text | Centered panel with title + reward + 2 buttons |
| `LevelSelectController` | (new) | Back button + ScrollView of level entries |
| `SettingsController` | (new) | Back button + 3 toggles (Music/SFX/Vibration) |

Controllers expose `BuildHierarchy(UITheme theme)` as `public virtual`. `UIBootstrap.EnsureBuilt` calls it after the empty `GameObject` panel is created and `[SerializeField]` fields are wired.

### 3.5 State machine

`UIManager.UIState` enum adds:
- `MainMenu` (existing)
- `Playing` (existing)
- `Paused` (existing)
- `Won` (existing)
- `Lost` (existing)
- `LevelSelect` (new)
- `Settings` (new)

`UIManager` public API additions:
- `ShowLevelSelect()` → `SetState(LevelSelect)`
- `ShowSettings()` → `SetState(Settings)`
- `StartGame()` → `LoadScene(Day03_ConveyorTest)` + `SetState(Playing)`
- `ShowMainMenu()` → `LoadScene(MainMenu)` + `SetState(MainMenu)`

### 3.6 Persistence services (unchanged)

| Service | PlayerPrefs key | Used by |
|---|---|---|
| `CoinService` | `FlowBlast.Coins` | MainMenu bottom coin label, Win reward |
| `LevelService.CurrentLevel` | `FlowBlast.CurrentLevel` | HUD level label |
| `LevelService.HighestUnlocked` | `FlowBlast.UnlockedLevel` | LevelSelect entry lock state |
| `SettingsService` (new) | `FlowBlast.Setting.<key>` | Settings toggles |

`SettingsService` API: `GetMusicEnabled()`, `SetMusicEnabled(bool)`, `GetSfxEnabled()`, `GetVibrationEnabled()`. Toggle UI calls these; gameplay reads them when needed (out of scope for now).

## 4. Component / scene layout

### 4.1 MainMenu scene (`Assets/Scenes/MainMenu.unity`)

```
Hierarchy:
  __FlowBlast_UI_Manager                  (UIManager, DontDestroyOnLoad)
  __FlowBlast_AutoCanvas                  (Canvas + CanvasScaler + GraphicRaycaster)
    __UIBootstrap                         (UIBootstrap, _stateAtRuntime = MainMenu, _theme = UITheme_300Mind)
    EventSystem                           (StandaloneInputModule)
    MainMenuPanel                         (MainMenuController, shownStates=[MainMenu])
    LevelSelectPanel                      (LevelSelectController, shownStates=[LevelSelect])
    SettingsPanel                         (SettingsController, shownStates=[Settings])
    LosePanel                             (WinLoseController, shownStates=[Lost])
```

WinPanel not included here — only Day03 needs it.

### 4.2 Day03 scene (existing)

Same hierarchy pattern, `_stateAtRuntime = Playing`. Includes HUDPanel + PausePanel + WinPanel + LosePanel.

### 4.3 Per-panel layout (portrait 1080×1920)

**MainMenuPanel:**
```
[Background sprite (clouds+hills) -- full screen]
[Title "FLOW BLAST"  anchored top, y = 1700, Oswald-Bold 96]
[PLAY button     y = 1100, w=540, h=110, icon=iconPlay + text]
[LEVELS button   y =  920]
[SETTINGS button y =  740]
[QUIT button     y =  560]
[Coin icon + label  anchored bottom, y = 120]
[Planet decoration góc trên trái]
```

**LevelSelectPanel:**
```
[Back button    anchored top-left, w=80, h=80]
[Title "SELECT LEVEL"   anchored top-center, y = 1820]
[ScrollView    y = 200..1700, vertical]
  Content (VerticalLayoutGroup, spacing 20)
    LevelButton "Level 01"  sprite rounded-rect + number + 3 stars
```

**SettingsPanel:**
```
[Back button    anchored top-left]
[Title "SETTINGS"  anchored top-center]
[Toggle "Music"     y = 1400]
[Toggle "SFX"       y = 1240]
[Toggle "Vibration" y = 1080]
[Credits text   y = 200, "Made with 300Mind UI Kit"]
```

**HUDPanel (always top safe area):**
```
[Pause icon button  anchored top-left, w=80, h=80, x=40, y=-40]
[Coin icon + label  anchored top-right]
[Progress bar + "1/5"  anchored top-center, y=-50, w=400, h=50]
[Level label "LEVEL 1"  anchored bottom-center, y=120]
```

**PausePanel (full-screen overlay):**
```
[Backdrop  full screen, color black alpha 0.55]
[Panel    center, w=720, h=900]
  Title "PAUSED"  top of panel
  RESUME button   y center
  RESTART button  y center - 130
  MAIN MENU button y center - 260
```

**WinPanel:**
```
[Overlay   full screen, gold tint 0.3 alpha]
[Panel    center, w=780, h=950]
  Title "VICTORY!"    Oswald-Bold 110
  Reward "+25" with coin icon
  NEXT LEVEL button
  MAIN MENU button
```

**LosePanel:** mirror of WinPanel with red tint and "TRY AGAIN" title.

## 5. Navigation flow

```
[App start]
    ↓ SceneManager.LoadScene("MainMenu")
[MainMenu state]
    ├─ PLAY       → LoadScene("Day03_ConveyorTest") → SetState(Playing)
    ├─ LEVELS     → SetState(LevelSelect)           [same scene]
    ├─ SETTINGS   → SetState(Settings)              [same scene]
    └─ QUIT       → Application.Quit()

[LevelSelect state]
    ├─ Back       → SetState(MainMenu)
    └─ Level tap  → LoadScene("Day03_ConveyorTest") → SetState(Playing)

[Playing state]  (Day03 scene)
    ├─ Pause      → SetState(Paused), Time.timeScale = 0
    ├─ Progress complete → ProgressWinListener fires Win() → SetState(Won)
    └─ (Lose not wired)

[Paused state]
    ├─ Resume     → SetState(Playing), Time.timeScale = 1
    ├─ Restart    → LoadScene("Day03_ConveyorTest")
    └─ Main Menu  → LoadScene("MainMenu") → SetState(MainMenu)

[Won state]   (timeScale stays 1)
    ├─ Next Level → LoadScene("Day03_ConveyorTest") → SetState(Playing)
    └─ Main Menu  → LoadScene("MainMenu") → SetState(MainMenu)
```

## 6. Files

### 6.1 New files

| File | Purpose |
|---|---|
| `Assets/Scripts/UI/UITheme_300Mind.cs` | ScriptableObject definition |
| `Assets/Scripts/UI/UIThemeBuilder.cs` | Static builder helpers |
| `Assets/Scripts/UI/LevelSelectController.cs` | LevelSelect panel |
| `Assets/Scripts/UI/SettingsController.cs` | Settings panel |
| `Assets/Scripts/UI/SettingsService.cs` | PlayerPrefs-backed toggle storage |
| `Assets/Scripts/UI/PopupController.cs` | Generic popup base (optional, used by future confirm dialogs) |
| `Assets/Scenes/MainMenu.unity` | New menu scene |
| `Assets/UI/UITheme_300Mind.asset` | Theme instance (created via menu) |
| `Assets/Editor/CreateUIThemeMenu.cs` | `FlowBlast/UI/Create 300Mind Theme` |

### 6.2 Modified files

| File | Change |
|---|---|
| `Assets/Scripts/UI/UIBootstrap.cs` | Add `[SerializeField] UITheme_300Mind _theme`, `_autoCreateThemeIfMissing`. Inject theme + call `themeBuilder.ApplyTo(panel)` after building skeleton. |
| `Assets/Scripts/UI/UIManager.cs` | Add `LevelSelect`, `Settings` enum values. Add `ShowLevelSelect`, `ShowSettings`, `StartGame`, scene constants. |
| `Assets/Scripts/UI/MainMenuController.cs` | Add `BuildHierarchy(UITheme)` that injects 4 buttons + title + coin label using `UIThemeBuilder`. |
| `Assets/Scripts/UI/HUDController.cs` | Add `BuildHierarchy(UITheme)` for top-bar layout. |
| `Assets/Scripts/UI/PauseController.cs` | Add `BuildHierarchy(UITheme)` for overlay + 3 buttons. |
| `Assets/Scripts/UI/WinLoseController.cs` | Add `BuildHierarchy(UITheme)` for Win/Lose panels. |
| `Assets/Scripts/UI/UIPanel.cs` | Expose `BuildHierarchy(UITheme theme)` virtual hook with default no-op. |

### 6.3 Not changed

- Existing gameplay scripts (Conveyor, Grid, GateMatcher, BoxTapMover).
- Existing services (CoinService, LevelService) — only their call sites in UI controllers change.

## 7. Implementation order

1. Write `UITheme_300Mind` ScriptableObject + `SettingsService` skeleton.
2. Write `UIThemeBuilder` static class with all helpers (panel, button, text, icon, progress, toggle, backdrop).
3. Write `CreateUIThemeMenu` editor script + manually wire the first `UITheme_300Mind.asset` instance in Editor (user clicks menu → picks sprites).
4. Extend `UIPanel` with `BuildHierarchy(UITheme)` virtual hook.
5. Extend `UIManager` with new states + API.
6. Update `MainMenuController`/`HUDController`/`PauseController`/`WinLoseController` to call `UIThemeBuilder`.
7. Write `LevelSelectController` + `SettingsController`.
8. Update `UIBootstrap` to inject theme + call `BuildHierarchy` on each panel.
9. Create `MainMenu.unity` scene + add bootstrap with `_stateAtRuntime = MainMenu`.
10. Update existing `Day03_ConveyorTest.unity` bootstrap to assign `_theme`.
11. Test end-to-end: Play MainMenu → Play → Pause → Resume → Win → Next → MainMenu.

## 8. Risks

- **Sprite slice mismatch:** 9-sliced sprites require Editor-side border configuration. If borders are wrong, panels stretch weirdly. Mitigation: validate via Play test, document border values in `CreateUIThemeMenu`.
- **Font import:** Oswald TTFs are imported but TMP_FontAsset must be created. Mitigation: theme menu creates default TMP_FontAssets from imported TTFs.
- **Scene persistence:** Saving `MainMenu.unity` requires Unity Editor — agents writing code don't save scenes. Mitigation: bootstrap is auto-build, so opening the scene fresh re-creates panels; `_theme` asset reference is preserved by GUID.
- **Safe area regression:** If HUD moves outside safe area on devices with notches, layout breaks. Mitigation: existing `UISafeArea` already handles this; HUDPanel lives under `HUD_SafeArea` parent.
- **Overlay panels on MainMenu scene:** LosePanel never appears from MainMenu but ships in scene. Mitigation: shownStates=[Lost] means it's hidden by default. Future-proof, no harm.

## 9. Acceptance criteria

- [ ] Empty `MainMenu.unity` scene + only `UIBootstrap` component → Play shows full MainMenu with 4 buttons, title, coin label — all from 300Mind sprites.
- [ ] Play button loads Day03 scene, HUD appears with pause/coin/progress.
- [ ] Pause button → Pause overlay → Resume returns to gameplay.
- [ ] Complete level 1 → Win panel with "+25" reward, coin balance increases by 25, persisted via PlayerPrefs.
- [ ] Quit button works in Editor standalone build.
- [ ] Settings toggles round-trip via PlayerPrefs.
- [ ] Mobile portrait 1080×1920 renders cleanly. Safe area respected (HUD doesn't overlap notch area).
- [ ] No compile errors, no null-ref on Play, no panel ever stuck invisible.

## 10. Open questions

None — design approved.