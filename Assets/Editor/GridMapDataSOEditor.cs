using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    /// <summary>
    /// Custom inspector for GridMapDataSO.
    /// Draws: base fields -> Grid Editor toolbar -> Box Color Palette -> quick actions
    /// -> Instantiate Prefabs to Board -> a clickable grid preview for painting cells.
    /// </summary>
    [CustomEditor(typeof(GridMapDataSO))]
    public class GridMapDataSOEditor : Editor
    {
        private GridMapDataSO _map;

        // Painting state
        private CellType _paintType = CellType.Wall;
        private BoxColor _selectedColor = BoxColor.Blue;

        // Random map generation range
        private int _wallPercentMin = 10;
        private int _wallPercentMax = 25;

        // Serialized properties (drawn manually so we control ordering/layout)
        private SerializedProperty _rowsProp;
        private SerializedProperty _colsProp;
        private SerializedProperty _mapNameProp;
        private SerializedProperty _difficultyProp;
        private SerializedProperty _availableColorsProp;
        private SerializedProperty _boxPrefabProp;
        private SerializedProperty _wallPrefabProp;
        private SerializedProperty _backgroundPrefabProp;
        private SerializedProperty _slotPrefabProp;
        private SerializedProperty _exitPrefabProp;
        private SerializedProperty _cellSizeProp;
        private SerializedProperty _cellSpacingProp;
        private SerializedProperty _boardOriginProp;
        private SerializedProperty _buildBackgroundProp;

        // Scene-only references (not saved in the SO)
        [SerializeField] private Transform _boardRoot;
        [SerializeField] private GameObject _backgroundPrefab;

        // Cached GUI styles (built lazily, first OnInspectorGUI call)
        private GUIStyle _cellLabelStyle;
        private GUIStyle _sectionHeaderStyle;
        private bool _stylesReady;

        private static readonly Dictionary<BoxColor, Color> ColorSwatches = new Dictionary<BoxColor, Color>
        {
            { BoxColor.Red,    new Color(0.62f, 0.10f, 0.16f) },
            { BoxColor.Blue,   new Color(0.20f, 0.55f, 0.95f) },
            { BoxColor.Green,  new Color(0.18f, 0.45f, 0.28f) },
            { BoxColor.Yellow, new Color(0.95f, 0.78f, 0.10f) },
            { BoxColor.Purple, new Color(0.45f, 0.20f, 0.55f) },
            { BoxColor.Orange, new Color(0.92f, 0.48f, 0.12f) },
        };

        private void OnEnable()
        {
            _map = (GridMapDataSO)target;
            _map.EnsureCellListSize();

            _rowsProp = serializedObject.FindProperty("Rows");
            _colsProp = serializedObject.FindProperty("Cols");
            _mapNameProp = serializedObject.FindProperty("MapName");
            _difficultyProp = serializedObject.FindProperty("Difficulty");
            _availableColorsProp = serializedObject.FindProperty("AvailableColors");
            _boxPrefabProp = serializedObject.FindProperty("BoxPrefab");
            _wallPrefabProp = serializedObject.FindProperty("WallPrefab");
            _backgroundPrefabProp = serializedObject.FindProperty("BackgroundPrefab");
            _slotPrefabProp = serializedObject.FindProperty("SlotPrefab");
            _exitPrefabProp = serializedObject.FindProperty("ExitPrefab");
            _cellSizeProp = serializedObject.FindProperty("CellSize");
            _cellSpacingProp = serializedObject.FindProperty("CellSpacing");
            _boardOriginProp = serializedObject.FindProperty("BoardOrigin");
            _buildBackgroundProp = serializedObject.FindProperty("BuildBackground");
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _cellLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            _stylesReady = true;
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();

            // ---- Basic fields -------------------------------------------------
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_rowsProp);
            EditorGUILayout.PropertyField(_colsProp);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(_map, "Resize Grid");
                _map.EnsureCellListSize();
                _map.EnsureExitExists();
            }

            EditorGUILayout.PropertyField(_mapNameProp);
            EditorGUILayout.PropertyField(_difficultyProp);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_availableColorsProp, new GUIContent("Colors"), true);

            EditorGUILayout.Space(12);
            DrawGridEditorToolbar();

            EditorGUILayout.Space(8);
            DrawColorPalette();

            EditorGUILayout.Space(8);
            DrawQuickActions();

            EditorGUILayout.Space(14);
            DrawInstantiatePrefabsSection();

            EditorGUILayout.Space(14);
            DrawGridPreview();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(_map);
        }

        // ------------------------------------------------------------------
        // Grid Editor toolbar (Empty / Box / Wall) - Exit removed in new gameplay flow
        // ------------------------------------------------------------------
        private void DrawGridEditorToolbar()
        {
            EditorGUILayout.LabelField("Grid Editor", _sectionHeaderStyle);

            // Exit is no longer paintable; boxes "leave" via top spline + container at 100%.
            CellType[] types = { CellType.Empty, CellType.Box, CellType.Wall };
            string[] labels = { "Empty", "Box", "Wall" };

            int current = Array.IndexOf(types, _paintType);
            if (current < 0)
            {
                _paintType = CellType.Empty;
                current = 0;
            }

            int chosen = GUILayout.Toolbar(current, labels, GUILayout.Height(26));
            if (chosen != current && chosen >= 0 && chosen < types.Length)
                _paintType = types[chosen];

            EditorGUILayout.HelpBox(
                "Exit cells are unused in the new flow. Boxes leave the grid upward into the bottom spline. " +
                "Use Paint to lay out Empty / Box / Wall only.",
                MessageType.None);
        }

        // ------------------------------------------------------------------
        // Box Color Palette
        // ------------------------------------------------------------------
        private void DrawColorPalette()
        {
            EditorGUILayout.LabelField("Box Color Palette", _sectionHeaderStyle);

            EditorGUILayout.BeginHorizontal();
            foreach (BoxColor color in _map.AvailableColors)
            {
                Color swatch = ColorSwatches.TryGetValue(color, out var c) ? c : Color.white;
                Rect rect = GUILayoutUtility.GetRect(34, 28, GUILayout.Width(34), GUILayout.Height(28));

                EditorGUI.DrawRect(rect, swatch);
                if (color == _selectedColor)
                {
                    // Simple selection outline
                    Handles.BeginGUI();
                    Handles.color = Color.white;
                    Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Color.white);
                    Handles.EndGUI();
                }

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedColor = color;
                    Event.current.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            Color selectedSwatch = ColorSwatches.TryGetValue(_selectedColor, out var sc) ? sc : Color.white;
            Rect previewRect = GUILayoutUtility.GetRect(22, 20, GUILayout.Width(22), GUILayout.Height(20));
            EditorGUI.DrawRect(previewRect, selectedSwatch);

            EditorGUILayout.LabelField(
                $"Selected: {_selectedColor}   #{ColorUtility.ToHtmlStringRGB(selectedSwatch)}",
                GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Reset", GUILayout.Width(70)))
            {
                _selectedColor = _map.AvailableColors.Count > 0 ? _map.AvailableColors[0] : BoxColor.Red;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------
        // Fill / Clear / Generate
        // ------------------------------------------------------------------
        private void DrawQuickActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill All Empty"))
            {
                Undo.RecordObject(_map, "Fill All Empty");
                FillAllEmptyWithBoxes();
                EditorUtility.SetDirty(_map);
            }
            if (GUILayout.Button("Clear Grid"))
            {
                Undo.RecordObject(_map, "Clear Grid");
                _map.ClearGrid();
                _map.EnsureExitExists();
                EditorUtility.SetDirty(_map);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            _wallPercentMin = EditorGUILayout.IntField("Wall % Min", _wallPercentMin, GUILayout.Width(160));
            _wallPercentMax = EditorGUILayout.IntField("Wall % Max", _wallPercentMax, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Generate Random Map"))
            {
                Undo.RecordObject(_map, "Generate Random Map");
                _wallPercentMin = Mathf.Clamp(_wallPercentMin, 0, 100);
                _wallPercentMax = Mathf.Clamp(_wallPercentMax, _wallPercentMin, 100);
                _map.GenerateRandomMap(_wallPercentMin, _wallPercentMax);
                EditorUtility.SetDirty(_map);
            }
        }

        private void FillAllEmptyWithBoxes()
        {
            for (int r = 0; r < _map.Rows; r++)
            {
                for (int c = 0; c < _map.Cols; c++)
                {
                    CellDataEntry cell = _map.GetCell(r, c);
                    if (cell.Type == CellType.Empty)
                        _map.SetCell(r, c, new CellDataEntry(CellType.Box, _selectedColor));
                }
            }
        }

        // ------------------------------------------------------------------
        // Instantiate Prefabs to Board
        // ------------------------------------------------------------------
        private void DrawInstantiatePrefabsSection()
        {
            EditorGUILayout.LabelField("Instantiate Prefabs to Board", _sectionHeaderStyle);

            EditorGUILayout.PropertyField(_wallPrefabProp, new GUIContent("Wall Prefab"));
            EditorGUILayout.PropertyField(_boxPrefabProp, new GUIContent("Box Prefab"));
            EditorGUILayout.PropertyField(_exitPrefabProp, new GUIContent("Exit Prefab"));
            EditorGUILayout.PropertyField(_slotPrefabProp, new GUIContent("Slot Prefab"));
            EditorGUILayout.PropertyField(_backgroundPrefabProp, new GUIContent("Background Prefab"));

            EditorGUILayout.Space(4);
            _boardRoot = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Board Root (Transform)"), _boardRoot, typeof(Transform), true);

            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Use Selected Hierarchy Object as Board Root"))
                {
                    var go = Selection.activeGameObject;
                    if (go != null) _boardRoot = go.transform;
                }
            }

            EditorGUILayout.PropertyField(_cellSizeProp, new GUIContent("Cell Size"));
            EditorGUILayout.PropertyField(_cellSpacingProp, new GUIContent("Cell Spacing"));
            EditorGUILayout.PropertyField(_boardOriginProp, new GUIContent("Board Origin"));
            EditorGUILayout.PropertyField(_buildBackgroundProp, new GUIContent("Build Background"));

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_boardRoot == null))
            {
                if (GUILayout.Button("Build Board (Instance Prefabs)"))
                    BuildBoard();
            }
            using (new EditorGUI.DisabledScope(_boardRoot == null))
            {
                if (GUILayout.Button("Clear Board Children"))
                    ClearBoardChildren();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_boardRoot == null))
            {
                if (GUILayout.Button("Recolor Existing Boxes"))
                    RecolorExistingBoxes();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Wire Boxes (Conveyor Refs)..."))
                FlowBlast.Gameplay.Grid.EditorTools.ConveyorBoxWiringWindow.Open();
            EditorGUILayout.EndHorizontal();
        }

        private GameObject ResolvePrefab(CellType type)
        {
            switch (type)
            {
                case CellType.Wall: return _map.WallPrefab;
                case CellType.Box: return _map.BoxPrefab;
                case CellType.Exit: return _map.ExitPrefab;
                default: return null;
            }
        }

        private void BuildBoard()
        {
            if (_boardRoot == null)
            {
                EditorUtility.DisplayDialog("Board Root Missing", "Assign a Board Root transform before building.", "OK");
                return;
            }

            ClearBoardChildren();

            for (int r = 0; r < _map.Rows; r++)
            {
                for (int c = 0; c < _map.Cols; c++)
                {
                    CellDataEntry cell = _map.GetCell(r, c);
                    if (cell.Type == CellType.Empty) continue;

                    GameObject prefab = ResolvePrefab(cell.Type);
                    if (prefab == null) continue;

                    Vector3 localPos = _map.BoardOrigin + new Vector3(
                        c * (_map.CellSize + _map.CellSpacing),
                        0f,
                        -r * (_map.CellSize + _map.CellSpacing));

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _boardRoot);
                    Undo.RegisterCreatedObjectUndo(instance, "Build Board");
                    instance.transform.localPosition = localPos;
                    instance.name = $"{cell.Type}_{r}_{c}";

                    if (cell.Type == CellType.Box)
                        ApplyBoxColor(instance, cell.Color);
                }
            }

            if (_map.BuildBackground && _map.BackgroundPrefab != null)
            {
                GameObject bg = (GameObject)PrefabUtility.InstantiatePrefab(_map.BackgroundPrefab, _boardRoot);
                Undo.RegisterCreatedObjectUndo(bg, "Build Board");
                bg.transform.localPosition = _map.BoardOrigin;
                bg.name = "Background";
            }
        }

        /// <summary>
        /// Best-effort tint of the spawned box prefab via its Renderer's material property block.
        /// Replace this with a call into your own Box component (e.g. box.Init(color)) if you have one.
        /// </summary>
        private void ApplyBoxColor(GameObject instance, BoxColor color)
        {
            if (!ColorSwatches.TryGetValue(color, out var c)) return;

            Renderer renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            // 1) Try MaterialPropertyBlock first (cheap, no allocation, but only works
            //    if the shader actually exposes a color property by that name).
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c);
            renderer.SetPropertyBlock(block);

            // 2) Fallback: clobber the material instance color directly. Using `material` (not
            //    `sharedMaterial`) clones the material per renderer, so we don't pollute the
            //    shared asset or other boxes. This works regardless of shader property names.
            Material[] mats = renderer.materials;
            bool dirty = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;

                if (mats[i].HasProperty("_BaseColor"))
                {
                    mats[i].SetColor("_BaseColor", c);
                    dirty = true;
                }
                if (mats[i].HasProperty("_Color"))
                {
                    mats[i].SetColor("_Color", c);
                    dirty = true;
                }
            }
            if (dirty)
            {
                renderer.materials = mats;
            }
        }

        private void ClearBoardChildren()
        {
            if (_boardRoot == null) return;

            for (int i = _boardRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(_boardRoot.GetChild(i).gameObject);
        }

        /// <summary>
        /// Reapplies the per-cell color to every Box_* child that already lives under the board root,
        /// based on the SO's current data. Useful when you change a box's color after building the board.
        /// </summary>
        private void RecolorExistingBoxes()
        {
            if (_boardRoot == null || _map == null) return;

            int touched = 0;
            foreach (Transform child in _boardRoot)
            {
                if (child == null || !child.name.StartsWith("Box_")) continue;

                // Parse "Box_R_C" -> (R, C)
                string[] parts = child.name.Split('_');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[1], out int r) || !int.TryParse(parts[2], out int c)) continue;

                int idx = r * _map.Cols + c;
                if (idx < 0 || idx >= _map.Cells.Count) continue;

                var cell = _map.Cells[idx];
                if (cell.Type != CellType.Box) continue;

                ApplyBoxColor(child.gameObject, cell.Color);
                EditorUtility.SetDirty(child.gameObject);
                touched++;
            }

            Debug.Log($"[FlowBlast] RecolorExistingBoxes: tinted {touched} box(es).");
        }

        // ------------------------------------------------------------------
        // Clickable grid preview ("Paint with: X")
        // ------------------------------------------------------------------
        private void DrawGridPreview()
        {
            EditorGUILayout.LabelField($"Paint with: {_paintType}", _sectionHeaderStyle);
            EditorGUILayout.LabelField("Left-click: paint cell | Right-click on BOX: open color picker for that box",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);

            const float cellPx = 34f;
            const float gap = 2f;

            for (int r = 0; r < _map.Rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < _map.Cols; c++)
                {
                    CellDataEntry cell = _map.GetCell(r, c);
                    Rect rect = GUILayoutUtility.GetRect(cellPx, cellPx, GUILayout.Width(cellPx), GUILayout.Height(cellPx));

                    DrawCell(rect, cell);
                    HandleCellInput(rect, r, c, cell);

                    GUILayout.Space(gap);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(gap);
            }
        }

        private void DrawCell(Rect rect, CellDataEntry cell)
        {
            Color bg = GetCellColor(cell);
            string label = GetCellLabel(cell);

            EditorGUI.DrawRect(rect, bg);

            // subtle border
            Handles.BeginGUI();
            Handles.color = new Color(0f, 0f, 0f, 0.5f);
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Handles.color);
            Handles.EndGUI();

            if (!string.IsNullOrEmpty(label))
                GUI.Label(rect, label, _cellLabelStyle);
        }

        private void HandleCellInput(Rect rect, int r, int c, CellDataEntry cell)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;

            bool isLeftPaint = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0;
            bool isRightClick = e.type == EventType.MouseDown && e.button == 1;

            if (isLeftPaint)
            {
                PaintCell(r, c);
                e.Use();
                Repaint();
            }
            else if (isRightClick && cell.Type == CellType.Box)
            {
                ShowBoxColorMenu(r, c);
                e.Use();
            }
        }

        private void PaintCell(int r, int c)
        {
            Undo.RecordObject(_map, "Paint Cell");

            // Exit is no longer paintable in the new gameplay flow.
            // Toolbar only offers Empty / Box / Wall, so we hit either Box or the default branch.
            if (_paintType == CellType.Box)
            {
                _map.SetCell(r, c, new CellDataEntry(CellType.Box, _selectedColor));
            }
            else if (_paintType == CellType.Exit)
            {
                // Defensive: if anyone ever sets _paintType = Exit programmatically,
                // wipe all exits and place one. (Toolbar no longer exposes this.)
                foreach (var idx in AllCellIndices())
                {
                    var e = _map.Cells[idx];
                    if (e.Type == CellType.Exit)
                        _map.Cells[idx] = CellDataEntry.Empty;
                }
                _map.SetCell(r, c, new CellDataEntry(CellType.Exit, BoxColor.Red));
            }
            else
            {
                _map.SetCellType(r, c, _paintType);
            }

            EditorUtility.SetDirty(_map);
        }

        private IEnumerable<int> AllCellIndices()
        {
            for (int i = 0; i < _map.Cells.Count; i++)
                yield return i;
        }

        private void ShowBoxColorMenu(int r, int c)
        {
            GenericMenu menu = new GenericMenu();
            foreach (BoxColor color in _map.AvailableColors)
            {
                BoxColor captured = color;
                menu.AddItem(new GUIContent(color.ToString()), false, () =>
                {
                    Undo.RecordObject(_map, "Change Box Color");
                    _map.SetCell(r, c, new CellDataEntry(CellType.Box, captured));
                    EditorUtility.SetDirty(_map);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private Color GetCellColor(CellDataEntry cell)
        {
            switch (cell.Type)
            {
                case CellType.Wall:
                    return new Color(0.05f, 0.05f, 0.05f);
                case CellType.Exit:
                    return new Color(0.12f, 0.75f, 0.35f);
                case CellType.Box:
                    return ColorSwatches.TryGetValue(cell.Color, out var c) ? c : Color.white;
                default:
                    return new Color(0.22f, 0.22f, 0.22f); // Empty
            }
        }

        private string GetCellLabel(CellDataEntry cell)
        {
            switch (cell.Type)
            {
                case CellType.Wall: return "#";
                case CellType.Exit: return "E";
                case CellType.Box: return "B";
                default: return "";
            }
        }
    }
}