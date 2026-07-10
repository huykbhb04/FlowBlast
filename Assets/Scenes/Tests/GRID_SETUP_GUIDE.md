# Grid System Setup Guide

## Overview

The grid system includes:
1. **GridData.cs** - Data model for grid map (cells, walls, boxes, exit)
2. **Pathfinding.cs** - BFS algorithm to find paths to exit
3. **GridManager.cs** - Manages grid visualization and box selection

## Setup in Day03 Scene

### Step 1: Create GridManager GameObject

1. Open `Assets/Scenes/Tests/Day03_ConveyorTest.unity`

2. In Hierarchy, right-click on `Gameplay` > Create Empty
   - Name: "GridManager"

3. Add Component: `GridManager` (FlowBlast.Gameplay.Grid.GridManager)

### Step 2: Configure GridManager

In Inspector, set:

**Grid Configuration:**
- Grid Rows: 8
- Grid Cols: 8
- Cell Size: 1.0
- Grid Offset: X: 0, Y: -3

**Visuals:**
- Leave prefabs empty for auto-generated cubes

**Colors:**
- Wall Color: RGB(51, 51, 51) - Dark gray
- Empty Color: RGB(26, 26, 38) - Dark blue
- Path Color: RGB(38, 64, 51) - Dark green
- Selectable Color: RGB(77, 128, 102) - Light green
- Exit Color: RGB(230, 179, 26) - Yellow/Gold

**Conveyor Connection:**
- Receive Point: Drag `BottomReceivePoint` transform here
- Bottom Spline: Drag `BottomSplinePath` GameObject here
- Bottom Spline Index: 0
- Move Speed: 2
- Loop Spline: Yes

### Step 3: Create Wall Objects (Optional Visual)

If you want 3D wall visuals:

1. Create simple cubes for walls
2. Or leave empty - walls will render as dark quads

### Step 4: Test

1. Play scene
2. You should see:
   - 8x8 grid with colored boxes
   - Some cells are dark (walls)
   - Yellow cell is the exit
   - Boxes with path to exit glow brighter
   - Click on selectable boxes to send them to conveyor

## How It Works

### BFS Pathfinding

When a box is selected or grid is initialized:

1. BFS starts from the Exit cell
2. Expands to all 4-directional neighbors (no diagonals)
3. Only traverses Empty, Box, or Exit cells (not Walls)
4. Marks all reachable cells as `IsPathToExit = true`
5. Boxes with path to exit become `IsSelectable = true`

### Box Selection Rules

A box can be selected ONLY if:
- It has a path to the exit (`IsPathToExit = true`)
- The path is not blocked by walls

### Path Recalculation

When a box is removed:
1. Box is removed from grid data
2. BFS recalculates all paths
3. New selectable boxes are highlighted
4. If a box becomes trapped (no path), it can no longer be selected

## Grid Coordinate System

```
Grid (row, col):
(0,0) ─────── (0,7)
  │            │
  │            │
(7,0) ─────── (7,7)

World X = col * cellSize + offset.x
World Z = row * cellSize + offset.y
```

## Color Legend

| Color | Meaning |
|-------|---------|
| Dark Gray | Wall (impassable) |
| Dark Blue | Empty (passable, no path) |
| Dark Green | Passable, has path to exit |
| Light Green | Selectable box (has path) |
| Yellow/Gold | Exit cell |
| Box Colors | Red, Blue, Green, Yellow, Purple, Orange |

## Customization

### Change Sample Map

Edit `GridData.cs` -> `CreateSampleMap()` method to create your own grid layout.

### Create Custom Grid Data

```csharp
GridMapData map = new GridMapData(5, 5);
map.SetWall(1, 1);           // Set wall
map.SetBox(0, 0, BoxColor.Red);  // Set colored box
map.SetExit(4, 4);            // Set exit
gridManager.SetGridData(map);
```

### Events

```csharp
// Subscribe to events
gridManager.OnBoxSelected += (cell) => {
    Debug.Log($"Selected: {cell.Color} at ({cell.Row}, {cell.Col})");
};

gridManager.OnBoxRemoved += (cell) => {
    Debug.Log($"Removed: {cell.Color}");
};

gridManager.OnGridUpdated += () => {
    Debug.Log("Grid paths recalculated");
};
```

## Troubleshooting

### Grid not visible
- Check GridManager is enabled
- Check cell size is not 0
- Check materials are assigned

### Boxes not clickable
- Check IsSelectable is true (boxes should glow)
- Check BoxClickHandler component exists on boxes
- Add Physics Raycast Camera if needed

### Path not working
- Ensure exit is set at valid position
- Check BFS algorithm in Pathfinding.cs
- Verify walls are marked as Wall type, not just visuals
