# Grid Map Designer Tool - ScriptableObject

## Tổng quan

Tool thiết kế map grid dùng ScriptableObject cho phép tạo và chỉnh sửa map trực quan trong Unity Editor.

## Files

| File | Purpose |
|------|---------|
| `GridMapDataSO.cs` | ScriptableObject chứa data map |
| `GridMapDataSOEditor.cs` | Custom editor với visual grid editor |
| `GridManager.cs` (updated) | Load map từ ScriptableObject |

## Cách sử dụng

### 1. Tạo Map mới

1. Trong Project window, right-click
2. Chọn: `Create > FlowBlast > Grid Map Data`
3. Đặt tên (ví dụ: `Map_Level01.asset`)

### 2. Thiết kế Map

Mở file ScriptableObject vừa tạo, bạn sẽ thấy:

**Map Info section:**
- Map Name: Tên hiển thị
- Difficulty: 1-5
- Rows/Cols: Kích thước grid
- Exit Position: Vị trí lối ra

**Paint Tool:**
- Chọn loại cell: Empty, Box, Wall, Exit
- Chọn màu box (chỉ áp dụng cho Box)

**Quick Actions:**
- Clear Grid: Xóa toàn bộ
- Fill with Walls: Toàn tường
- Fill with Boxes: Toàn hộp
- Random Map: Tạo map ngẫu nhiên
- Validate: Kiểm tra map hợp lệ

**Grid Editor (Visual):**
- **Left Click:** Paint cell với type/color đã chọn
- **Shift + Click:** Erase (set Empty)
- **Right Click:** Đặt Exit Position

**Statistics:**
- Hiển thị số lượng từng loại cell

**Validation:**
- Cảnh báo nếu không có box nào reachable

### 3. Sử dụng trong Scene

1. Mở scene Day03
2. Chọn GameObject `GridManager`
3. Trong Inspector, kéo ScriptableObject map vào field `Map Data SO`
4. Play scene → Grid sẽ load từ ScriptableObject

### 4. Load Map động (Runtime)

```csharp
using FlowBlast.Gameplay.Grid;

public class MapLoader : MonoBehaviour
{
    [SerializeField] private GridMapDataSO[] allMaps;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private int currentMapIndex = 0;
    
    public void LoadNextMap()
    {
        currentMapIndex = (currentMapIndex + 1) % allMaps.Length;
        gridManager.LoadMap(allMaps[currentMapIndex]);
    }
}
```

## Tính năng

### Random Map Generator
- Tùy chỉnh tỷ lệ wall và box
- Sử dụng available colors đã chọn

### Validation
- Tự động kiểm tra map có hợp lệ
- Cảnh báo boxes bị block

### OnValidate
- Tự động clamp rows/cols
- Tự động đảm bảo exit cell type
- Tự động resize cells list

## Sample Maps

Tạo sẵn một số map mẫu:

**Map 1: Easy (8x8)**
- Ít walls (~15%)
- Nhiều boxes có thể chọn

**Map 2: Medium (8x8)**
- Nhiều walls (~25%)
- Một số boxes bị trap

**Map 3: Hard (10x10)**
- Grid lớn hơn
- Walls phức tạp

## Workflow đề xuất

1. Tạo folder `Assets/Maps/`
2. Tạo các map asset trong folder này
3. Tạo `MapDatabase` ScriptableObject chứa array of maps
4. Sử dụng để load level tuần tự

## Tips

- Dùng **Random Map** để prototype nhanh
- Dùng **Validate** thường xuyên khi edit
- **Shift+Click** để xóa nhanh
- **Right Click** để đặt exit nhanh
- Mở rộng bằng cách thêm custom cell types

## Mở rộng

### Thêm Cell Type mới

1. Thêm vào enum `CellType` trong `GridData.cs`
2. Update `GridMapDataSOEditor.cs` paint tool
3. Update visualization

### Thêm validation rules

```csharp
public bool IsValidMap()
{
    // Add custom rules
    return base.IsValidMap() && /* your rules */;
}
```

### Thêm metadata

Thêm fields vào `GridMapDataSO`:
- Music
- Background
- Time limit
- Star conditions
