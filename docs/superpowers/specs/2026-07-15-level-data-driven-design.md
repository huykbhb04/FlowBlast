# Level Data-Driven (ScriptableObject) Design

**Date:** 2026-07-15
**Status:** Approved

---

## 1. Overview

Extend `GridMapDataSO` thành full `LevelConfig` — một ScriptableObject duy nhất chứa toàn bộ data cần thiết để dựng màn chơi: grid layout, conveyor parameters, slot config, và target. `LevelLoader` đọc SO và gọi setup cho từng subsystem.

---

## 2. Architecture

```
GridMapDataSO (LevelConfig)
       │
       ├── Grid Map          ← existing Cells[], AvailableColors
       ├── Conveyor Config   ← BlockSpeed, BlocksPerSecond, BlocksPerCluster
       ├── Slot Config       ← SlotCount, SlotCapacity
       └── Target Config     ← TargetBoxCount (auto = total boxes)

LevelLoader
       │
       ├── GridManager.BuildGrid(config)
       ├── SplineConveyor.SetupFromConfig(config)
       ├── BottomRayManager.SetupSlots(config.SlotCount)
       └── GameProgressHUD.Bind(config.TargetBoxCount)
```

---

## 3. GridMapDataSO Changes (Extend)

### Existing fields (giữ nguyên)
- `Rows`, `Cols`
- `Cells` — grid cell data (Wall, Box with Color, Empty)
- `MapName`, `Difficulty`
- `AvailableColors`
- Prefab references

### New fields

```csharp
[Header("Conveyor")]
public float BlockSpeed = 3f;          // units/sec — tốc độ ball trên conveyor
public float BlocksPerSecond = 1f;     // spawn rate
public int BlocksPerCluster = 20;     // balls per color group

[Header("Slot & Target")]
[Range(1, 8)] public int SlotCount = 4;  // số slot bottom
public float SlotCapacity = 100f;         // % capacity mặc định

[Header("Block Sequence")]
public bool DeriveBlockSequenceFromGrid = true;
public List<BoxColor> BlockSequenceOverride; // null = auto from grid

[Header("Target")]
public int TargetBoxCount = 0;  // 0 = auto = đếm boxes trong Cells
```

### Helper methods

```csharp
// Đếm số box theo màu trong grid
public Dictionary<BoxColor, int> GetBoxCountByColor();

// Build ball sequence: Red x5 → Blue x3 → Green x2 → ...
public List<BoxColor> BuildBlockSequence();
```

---

## 4. Block Sequence Logic

**Derive from grid (default):** duyệt `Cells`, đếm số box mỗi màu → generate sequence.

```
Grid: Red=5, Blue=3, Green=2
→ Sequence: [R,R,R,R,R, B,B,B, G,G]
```

**Override:** nếu `BlockSequenceOverride` có data, dùng trực tiếp (cho custom levels).

---

## 5. LevelLoader

```csharp
public class LevelLoader : MonoBehaviour
{
    [SerializeField] private GridMapDataSO _levelConfig;

    public void LoadLevel(GridMapDataSO config)
    {
        _levelConfig = config;

        // 1. Build grid
        _gridManager.BuildGrid(config);

        // 2. Setup conveyor
        _conveyor.SetupFromConfig(config);

        // 3. Setup slots
        _bottomRayManager.SetupSlots(config.SlotCount, config.SlotCapacity);

        // 4. Bind HUD
        int target = config.TargetBoxCount > 0
            ? config.TargetBoxCount
            : config.GetBoxCountByColor().Values.Sum();
        _hud.Bind(target);
    }

    public void ReloadLevel() => LoadLevel(_levelConfig);
}
```

---

## 6. Files

### Modify
- `Assets/Scripts/Gameplay/Grid/GridMapDataSO.cs` — thêm fields + helper methods

### Create
- `Assets/Scripts/Gameplay/LevelLoader.cs` — load level từ SO
- `Assets/ScriptableObjects/Levels/Level_01_Easy.asset` — SO mẫu dễ
- `Assets/ScriptableObjects/Levels/Level_02_Medium.asset` — SO mẫu trung bình
- `Assets/ScriptableObjects/Levels/Level_03_Hard.asset` — SO mẫu khó

### Update
- `Assets/Scripts/Gameplay/Conveyor/SplineConveyor.cs` — thêm `SetupFromConfig(GridMapDataSO)`
- `Assets/Scripts/Gameplay/Conveyor/BottomRayManager.cs` — thêm `SetupSlots(count, capacity)`
- `Assets/Scripts/Gameplay/Grid/GridManager.cs` — đã có `BuildGrid`, check compatibility

---

## 7. SO Level Samples

### Level_01_Easy
- Grid: 4x5, ~8 boxes, 2 walls
- Colors: Red, Blue, Green, Yellow
- Speed: 3, BlocksPerSecond: 0.8, Cluster: 15
- Slots: 4, Capacity: 100%
- Auto target = 8 boxes

### Level_02_Medium
- Grid: 6x6, ~15 boxes, 5 walls
- Colors: 5 màu
- Speed: 4, BlocksPerSecond: 1.0, Cluster: 20
- Slots: 6, Capacity: 100%

### Level_03_Hard
- Grid: 8x8, ~25 boxes, 10 walls
- Colors: 6 màu
- Speed: 5, BlocksPerSecond: 1.2, Cluster: 25
- Slots: 8, Capacity: 100%

---

## 8. Acceptance Criteria

- [ ] Mỗi SO level dựng được màn hoàn chỉnh (grid + conveyor + slots + target)
- [ ] Đổi `BlockSpeed` trong SO → conveyor chạy nhanh/chậm ngay
- [ ] Đổi `SlotCount` trong SO → số slot thay đổi
- [ ] Block sequence tự derive đúng từ grid
- [ ] `LevelLoader.ReloadLevel()` reset lại toàn bộ scene
- [ ] Không sửa code chỉ để thay đổi level
