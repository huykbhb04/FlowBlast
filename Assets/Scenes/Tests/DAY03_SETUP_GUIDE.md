# Day03 Complete Setup Guide

## Part 1: Conveyor System (Already Done)

### Top Conveyor
- 80 blocks auto-spawn and loop on `TopSplinePath1`
- Spacing: 0.76 units
- Speed: 2 m/s

### Bottom Conveyor (Tap-to-Move)
- 3 debug boxes with `BoxTapMover`
- Click box → jump to receive point → move on `BottomSplinePath`
- Loop continuous

### Gate & Receive Points
- `TopGatePoint`: (X: -0.02, Y: 0.48, Z: 2.6)
- `BottomReceivePoint`: (X: -2.48, Y: -0.71, Z: 1.11)

---

## Part 2: Grid System (New)

### Scripts Created

| File | Purpose |
|------|---------|
| `GridData.cs` | Data model: grid cells, walls, boxes, exit |
| `Pathfinding.cs` | BFS algorithm to find paths to exit |
| `GridManager.cs` | Grid visualization and box selection |
| `GridSetup.cs` | Auto-setup helper |

### Step-by-Step Setup

#### Step 1: Create GridManager GameObject

1. Open `Assets/Scenes/Tests/Day03_ConveyorTest.unity`

2. In Hierarchy, right-click `Gameplay` > Create Empty > Name: "GridManager"

3. Add Component: `GridManager`

#### Step 2: Configure GridManager

In Inspector, configure:

```
Grid Configuration:
├── Grid Rows: 8
├── Grid Cols: 8
├── Cell Size: 1.0
└── Grid Offset: (X: 0, Y: -5)

Visuals:
├── Cell Prefab: (leave empty)
├── Box Prefab: (leave empty)
├── Wall Prefab: (leave empty)
└── Exit Prefab: (leave empty)

Colors:
├── Wall Color: (51, 51, 51) - Dark Gray
├── Empty Color: (26, 26, 38) - Dark Blue
├── Path Color: (38, 64, 51) - Dark Green
├── Selectable Color: (77, 128, 102) - Light Green
└── Exit Color: (230, 179, 26) - Yellow/Gold

Conveyor Connection:
├── Receive Point: [Drag BottomReceivePoint here]
├── Bottom Spline: [Drag BottomSplinePath here]
├── Bottom Spline Index: 0
├── Move Speed: 2
└── Loop Spline: ✓ (checked)
```

#### Step 3: Position the Grid

Move GridManager transform:
- Position: (X: 0, Y: 0, Z: 0)

The grid will appear at world position based on offset.

#### Step 4: Test

Play scene. You should see:
- 8x8 grid with colored boxes
- Some dark cells (walls)
- Yellow cell (exit)
- Boxes with path glow brighter
- Click boxes to send to conveyor

---

## How the Grid System Works

### Data Model

```
GridMapData:
├── Cells[8,8] - 2D array of GridCell
├── ExitPosition - Vector2Int
└── Each Cell has:
    ├── Type: Empty | Box | Wall | Exit
    ├── Color: Red | Blue | Green | Yellow | Purple | Orange
    ├── IsPathToExit: bool
    └── IsSelectable: bool
```

### BFS Pathfinding

```
1. Start BFS from Exit cell
2. Explore 4-directional neighbors
3. Only traverse: Empty, Box, Exit (NOT Wall)
4. Mark all reachable cells as IsPathToExit = true
5. Mark boxes with path as IsSelectable = true
```

### Selection Rules

**Box can be selected IF:**
- It has a path to exit (not blocked by walls)
- IsPathToExit = true

**When box is removed:**
1. Box removed from grid
2. BFS recalculates all paths
3. New boxes may become trapped

---

## Grid Coordinate System

```
Grid (row, col):
(0,0) ─────── (0,7)
  │            │
  │            │
(7,0) ─────── (7,7)

World X = col * cellSize + offset.x - gridCols * cellSize / 2
World Z = row * cellSize + offset.y - gridRows * cellSize / 2
```

---

## Sample Map Layout

The `CreateSampleMap()` creates this layout:

```
R\C 0 1 2 3 4 5 6 7
 0  B B W B B . B B   (W=Wall, B=Box, .=Empty, E=Exit)
 1  B B W B B B B B
 2  . B W . B B B B
 3  B B W . W . B E
 4  B . W . B B B B
 5  B B B . B B B B
 6  B B B . B . B B
 7  B W B B B B B B

Color varies per box
Exit at (7, 7)
```

---

## Events

```csharp
gridManager.OnBoxSelected += (cell) => {
    Debug.Log($"Selected: {cell.Color}");
};

gridManager.OnBoxRemoved += (cell) => {
    Debug.Log($"Removed box");
};

gridManager.OnGridUpdated += () => {
    Debug.Log("Paths recalculated");
};
```

---

## Troubleshooting

### Grid not visible
- Check GridManager is enabled
- Verify cell size > 0
- Check offset positions grid in camera view

### Boxes not clickable
- Should glow green if selectable
- Check BoxClickHandler exists on boxes
- Add EventSystem to scene if missing

### Path not working
- Verify exit is set
- Check BFS in Pathfinding.cs
- Walls must be marked as Wall type

### Performance
- Reduce grid size for mobile
- Consider object pooling for boxes
