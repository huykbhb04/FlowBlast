# FlowBlast

Một game puzzle ghép màu được phát triển bằng Unity, nơi người chơi cần ghép các khối màu từ lưới vào các rãnh băng chuyền bên dưới.

## Mục lục

- [Giới thiệu](#giới-thiệu)
- [Tính năng chính](#tính-năng-chính)
- [Cấu trúc dự án](#cấu-trúc-dự-án)
- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Cài đặt](#cài-đặt)
- [Cách chơi](#cách-chơi)
- [Kiến trúc Gameplay](#kiến-trúc-gameplay)
- [Hệ thống UI](#hệ-thống-ui)
- [Âm thanh](#hệ-thống-âm-thanh)
- [Lưu trữ dữ liệu](#lưu-trữ-dữ-liệu)

## Giới thiệu

FlowBlast là một game puzzle hành động kết hợp giữa:
- **Lưới puzzle chiến lược** - nơi các khối màu được sắp xếp
- **Băng chuyền tự động** - vận chuyển khối đến cổng
- **Hệ thống booster** - hỗ trợ người chơi vượt qua thử thách

## Tính năng chính

### Gameplay
- **Hệ thống lưới Grid** với nhiều loại ô (Box, Empty, Obstacle, Trapped)
- **Băng chuyền Spline** với animation mượt mà
- **Hệ thống cổng (Gate)** để hoàn thành rãnh
- **Pathfinding** tự động xác định các ô có thể chọn

### Boosters
| Booster | Mô tả |
|---------|-------|
| **Shuffle** | Xáo trộn vị trí các khối trên lưới |
| **Hand** | Cho phép chọn bất kỳ khối nào (bỏ qua ràng buộc) |
| **Magnet** | Hút khối cùng màu từ trên xuống |

### UI/UX
- **Main Menu** với Play, Level Select, Settings
- **HUD** hiển thị tiến trình và boosters
- **Popups** cho Win/Lose/Pause/BuyBooster
- **Hệ thống Theme** linh hoạt (300Mind UI Kit)

### Audio
- **Nhạc nền** loop theo scene
- **SFX** cho click, button, win, lose, booster
- **Điều khiển âm lượng** riêng biệt cho music và SFX

## Cấu trúc dự án

```
Assets/
├── Scripts/
│   ├── Core/                    # GameState, GameStateMachine
│   ├── Common/                   # ComponentPool (object pooling)
│   ├── Data/                     # SaveData
│   ├── Gameplay/
│   │   ├── Boosters/             # IBooster, ShuffleBooster, HandBooster, MagnetBooster
│   │   ├── Conveyor/             # BottomRayManager, BoxSlot, BoxContainer, SplineConveyor
│   │   └── Grid/                # GridManager, GridMapData, Pathfinding, BoardBoxRegistry
│   ├── Managers/
│   │   ├── AudioManager.cs       # Singleton audio system
│   │   ├── SaveManager.cs        # JSON persistence
│   │   └── PopupManager.cs       # Popup orchestration
│   └── UI/
│       ├── Controllers/          # MainMenuController, HUDController, LevelSelectController
│       ├── Popup/               # WinPopup, LosePopup, PausePopup, BuyBoosterPopup
│       ├── Theme/               # UITheme_300Mind, UIThemeBuilder
│       └── UIBootstrap.cs       # Panel initialization
├── Scenes/
│   ├── Main.unity                # Gameplay scene
│   ├── MainMenu.unity            # Menu scene
│   └── Tests/                    # Test scenes
└── 300Mind/2D Game UI Kit/       # UI assets
```

## Yêu cầu hệ thống

- **Unity 2022.3 LTS** hoặc mới hơn
- **URP** (Universal Render Pipeline)
- **Input System Package**
- **Splines Package** (com.unity.splines)

## Cài đặt

1. Clone repository
2. Mở project bằng Unity Editor
3. Unity sẽ tự động resolve packages từ `manifest.json`
4. Build settings:
   - Scene 0: `Assets/Scenes/MainMenu.unity`
   - Scene 1: `Assets/Scenes/Main.unity`

## Cách chơi

### Luật chơi cơ bản
1. Nhấn vào khối màu trên lưới để di chuyển xuống băng chuyền
2. Khối sẽ tự động di chuyển dọc theo spline
3. Ghép đủ số khối cùng màu vào cổng tương ứng để hoàn thành rãnh
4. Hoàn thành tất cả các rãnh để thắng
5. Thua khi tất cả rãnh đều đầy mà không hoàn thành được

### Điều khiển
- **Click/Tap** vào khối để chọn
- **Pause Button** trên HUD để tạm dừng
- **Booster buttons** để sử dụng boosters

## Kiến trúc Gameplay

### Game State Machine

```
Loading → MainMenu → Playing ↔ Paused
                        ↓
                    Win / Lose
```

### Grid System

```
GridManager
├── GridMapData (grid state mirror)
├── Pathfinding (selectable boxes algorithm)
├── BoardBoxRegistry (runtime box tracking)
└── BoxClickHandler (input routing)
```

### Conveyor System

```
BottomRayManager
├── BoxSlot[4] (slot positions on spline)
├── SplineConveyor (movement along path)
└── GateMatcher (color matching logic)
```

### Box Lifecycle

```
Grid Cell → Tap → Jump Animation → Slot Placement
                                      ↓
                              Spline Movement
                                      ↓
                              Gate Matching → Exit Animation
```

## Hệ thống UI

### Panels
- `UIPanel` - Base class cho tất cả panels
- `MainMenuController` - Menu chính
- `HUDController` - HUD gameplay
- `LevelSelectController` - Chọn level
- `PauseController` / `WinLoseController` - Overlays

### Popup System
```
PopupManager
├── PausePopup
├── WinPopup
├── LosePopup
└── BuyBoosterPopup
```

### Theme System
- `UITheme_300Mind` - Asset chứa colors, fonts, sprites
- `UIThemeBuilder` - Script tạo UI từ theme
- `UIGridBuilder` - Xây dựng grid UI động

## Hệ thống âm thanh

### AudioManager (Singleton)
- 2 AudioSources: Music + SFX
- Tích hợp `SaveManager` cho settings persistence
- Audio clips được định nghĩa trong `AudioLibrarySO`

### AudioIds
```csharp
ButtonClick, Win, Lose, Booster, BoxPlace, ...
```

## Lưu trữ dữ liệu

### SaveData
```csharp
- CurrentLevel: int
- Coins: int
- IsMusicOn: bool
- IsSfxOn: bool
- BoosterInventory: Dictionary<BoosterType, int>
```

### Persistence
- JSON file trong `Application.persistentDataPath`
- Tự động save khi thay đổi settings
- Manual save/load qua `SaveManager`

## Scripts quan trọng

| File | Mô tả |
|------|-------|
| `GameStateMachine.cs` | FSM quản lý trạng thái game |
| `GridManager.cs` | Quản lý lưới puzzle và animation khối |
| `BottomRayManager.cs` | Quản lý băng chuyền và slots |
| `LevelLoader.cs` | Load level config và setup game |
| `BoosterController.cs` | Điều khiển sử dụng boosters |
| `UIBootstrap.cs` | Khởi tạo UI hierarchy |

## Development

### Coding Conventions
- Namespace: `FlowBlast.{Module}`
- MonoBehaviour classes: PascalCase
- Events: `On[Event]` pattern (e.g., `OnBoardStateChanged`)
- SerializeField: `_camelCase`

### Testing
- Test scenes trong `Assets/Scenes/Tests/`
- `Day03_ConveyorTest.unity` - Test băng chuyền

## Credits

- **300Mind** - 2D Game UI Kit
- **TextMesh Pro** - Advanced Text
- **Unity Splines** - Spline system
