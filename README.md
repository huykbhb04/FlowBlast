# FlowBlast

Mini-game 2D phong cách **Conveyor / Flow**: bóng liên tục chạy trên băng chuyền trên, người chơi tap các hộp ô nhảy xuống băng chuyền dưới để chặn bóng cùng màu, hoàn thành 100% cả 5 hộp để thắng.

Dự án build bằng **Unity 6 + Unity Splines**.

---

## 1. Yêu cầu môi trường

| | |
|---|---|
| Unity Editor | 6.0 trở lên |
| Render pipeline | Universal Render Pipeline (URP) |
| Packages (Bao gồm sẵn trong `Packages/manifest.json`) | `com.unity.splines` (Unity Splines), `com.unity.2d.tilemap`, `com.unity.shadergraph`, `com.unity.render-pipelines.universal`, `com.unity.textmeshpro` |
| Platform test | Editor Play (PC/Mac) + target Android (đã có `Assets/Plugins/Android`) |

Không cần cài thêm package ngoài — mở Project là build được.

---

## 2. Chạy nhanh (3 bước)

1. Mở Unity Hub → add project trỏ vào thư mục gốc (folder chứa `Assets/`, `Packages/`, `ProjectSettings/`).
2. Đợi lần đầu import (Library chưa có sẽ build từ `Packages/manifest.json`, mất 1-3 phút).
3. Mở scene **`Assets/Scenes/Tests/Day03_ConveyorTest.unity`**, nhấn **Play**.

Khi Play, kéo xem các khối đang rơi từ băng chuyền trên. Tap vào từng hộp (`Box`) trên grid để gửi hộp xuống băng chuyền dưới — bóng cùng màu sẽ nhảy vào hộp tăng 20 % mỗi lần, đủ 5 lần = hoàn thành.

---

## 3. Cấu trúc Project

```
Assets/
├── Art/
│   ├── Model/        # FBX (Slot, Box, Conveyor, Hidden, Ice, ...)
│   └── Texures/      # PNG texture cho conveyor, hidden, color
├── Editor/
│   └── GridMapDataSOEditor.cs   # Editor trực quan thiết kế map (Custom Inspector)
├── Maps/             # ScriptableObject Level data (GridMapDataSO assets, vd. Level_01.asset)
├── Scenes/
│   └── Tests/        # Day03_ConveyorTest.unity + 2 file hướng dẫn setup
├── ScriptableObjects/
│   └── GRID_MAP_DESIGNER_GUIDE.md
└── Scripts/
    └── Gameplay/
        ├── Conveyor/   # Core loop (xem mục 4)
        └── Grid/       # Grid data, Pathfinding, GridMapDataSO, GridManager, GridSetup

Packages/
└── manifest.json      # Dependency khai báo

ProjectSettings/        # Tag, Layer, Quality, Build target
```

---

## 4. Core scripts (đọc trước khi review)

### Conveyor (loop chính)
| File | Vai trò |
|---|---|
| `SplineConveyor.cs` | Chạy bóng trên đường spline đặt trong scene |
| `GateMatcher.cs` | Bóng đến cửa → kiểm tra box ở slot dưới → nếu trùng màu: bóng nhảy vào box (tăng 20 %) |
| `BoxTapMover.cs` | Tap box trên grid → bay xuống slot idlest trên belt dưới |
| `BottomRayManager.cs` | 4 slot cố định dưới belt, quản lý chiếm-chỗ; giới hạn tối đa 4 box |
| `BottomConveyorController.cs` | Đẩy box đầy 100 % về cổng `BoxExitAnimator` |
| `BoxContainer.cs` | Lưu progress (0/100), màu, `OnCompleted` event |
| `GameProgressHUD.cs` | TMP text góc trái: `Boxes: X/N`; tự đếm theo `TotalBoxes` / `Selectable` / `Manual` |
| `BoxProgressDisplay.cs` | Thanh progress nổi trên mỗi box (khi đầy → full + animate ra) |
| `BoxSlot.cs`, `BoxExitAnimator.cs`, `ConveyorColoredBlock.cs` | Phụ trợ: slot, exit dứt box, mesh/block hiển thị màu |
| `ConveyorAutoBootstrap.cs` | Auto-instance box lúc Awake nếu editor chưa drag tay |
| `RayInputBlocker.cs` | Chặn raycast UI đè lên grid khi cần |

### Grid (luật chọn hộp theo đường ra)
| File | Vai trò |
|---|---|
| `GridMapDataSO.cs` | ScriptableObject: `Rows`, `Cols`, `GridCell[,]`. Mỗi cell: loại (Box, Wall, Empty, Hidden), màu, `IsSelectable` |
| `GridManager.cs` | Runtime: instantiate prefab theo map, gắn vào `Cells[r,c]`, expose `GetGridMap()` |
| `GridData.cs` | `BoxColor` enum, `BoxType` enum, `GridCell` struct (HasBox, IsSelectable, ...) |
| `Pathfinding.cs` | BFS 4 hướng từ `Exit` cell; set `IsSelectable=true` cho box cell còn thông đường, `false` cho box kẹt sau wall |
| `GridSetup.cs` | Helper build `Level_01` lúc runtime nếu asset chưa gắn |
| `GridMapDataSOEditor.cs` (`Assets/Editor/`) | Custom Inspector vẽ bảng grid + paint cell type/color trong Inspector |

### Maps
- **`Assets/Maps/Level_01.asset`** — level 4×5 mặc định: 4 box, 1 wall, 1 exit. Tap box nào cũng reachable.

---

## 5. Hướng dẫn thiết kế level mới

1. Project window → chuột phải vào `Assets/Maps/` → Create → **FlowBlast → Grid Map Data**.
2. Click asset → Inspector hiện grid 4–6 × 5–7 ô. Pick tool, click ô để sửa:
   - `Empty` — không có gì
   - `Box` — có box, chọn `BoxColor` (Yellow / Red / Blue / Green)
   - `Wall` — chặn path
   - `Exit` — ô thoát của băng chuyền dưới
   - `Hidden` — ô ẩn
   - `Ice` — ô băng (kẹp tuỳ chỉnh)
3. Nhấn **Reevaluate Selectable** → script BFS quét và tự tô xanh các box còn đường ra, đỏ các box kẹt sau wall.
4. Kéo asset vào field **`Grid Map Data`** của `GridManager` trong scene.

Xem chi tiết trong **`Assets/ScriptableObjects/GRID_MAP_DESIGNER_GUIDE.md`** và **`Assets/Editor/GridMapDataSOEditor.cs`** (top of file có chú thích).

---

## 6. Đóng gói Play Mode Test (tự kiểm thử)

Hiện chưa có EditMode/PlayMode test trong repo. Cách test thủ công nhanh nhất:

1. Mở scene `Day03_ConveyorTest.unity`.
2. Play → quan sát bóng rơi đều trên belt trên.
3. Tap lần lượt 4 box → kiểm tra `Console` hiện các dòng:
   - `Box tap registered` (từ `BoxTapMover`)
   - `Now occupied: N/4` (từ `BottomRayManager.PlaceBox`)
   - `MATCH Yellow -> slot 1 progress=20` (từ `GateMatcher`)
   - `Box completed (1/4)` khi đạt 100 %
   - HUD update `Boxes: 1/5`, `Boxes: 2/5`, ..., `Boxes: 4/5`
4. Nếu dòng nào sai → đọc stack trace → report lại.

Yêu cầu đã được implement trong file nào, xem mục 4.

---

## 7. Build Standalone / Android

1. `File → Build Profiles…`
2. Platform: chọn `Android` hoặc `Windows`.
3. Scenes in Build: kéo `Day03_ConveyorTest.unity` vào.
4. Build → kiểm tra `.apk` hoặc `.exe`.

(Chưa build mobile trong tuần 1 — đề xuất làm cuối tuần 2.)

---

## 8. Đã biết (limitation tuần 1)

- Chưa có level fail state (bóng trôi qua cửa không hợp => biến mất, không penalty).
- Chưa có âm thanh.
- Băng chuyền dưới hiện chỉ nhận tối đa 4 box đồng thời (giới hạn design).
- Auto-test (Unity Test Framework) chưa cài đặt.
- HUD chỉ đếm box cell ở grid; chưa đếm "box hoàn thành lần 2" nếu level cho phép reset.

---

## 9. Tuần 1 — Mốc review

**Slice cơ bản (đang chốt):**

- ✅ Ghép spline conveyor + grid map + luật chọn hộp theo đường ra.
- ✅ Khối tới cửa gặp hộp cùng màu ở vị trí nhận → nhảy vào hộp (tăng dần tới 100 %).
- ✅ Hộp đạt 100 % → hoàn thành, tăng tiến độ; giới hạn tối đa 4 hộp trên băng chuyền dưới.
- 🔧 Tự test, fix bug — fix nốt HUD total, fix build lỗi compile.
- 🔧 README, demo video, báo cáo tuần.

Mentor đánh giá baseline (khả năng dùng Splines), độ sạch code, khả năng tiếp thu.

---

## 10. Đóng góp / Liên hệ

- Branch làm việc: mặc định (`master`/`main`).
- Convention: code trong `Assets/Scripts/...`, mọi file mới phải có `.meta`.
- Game logic tách hẳn khỏi Editor script (`Assets/Editor/` chỉ dùng cho Inspector tool).
