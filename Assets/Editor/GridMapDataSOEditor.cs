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
        private BoxColor _selectedColor = BoxColorUtility.DefaultColor;

        // Random map generation range
        private int _wallPercentMin = 10;
        private int _wallPercentMax = 25;

        // Serialized properties (drawn manually so we control ordering/layout)
        private SerializedProperty _rowsProp;
        private SerializedProperty _colsProp;
        private SerializedProperty _mapNameProp;
        private SerializedProperty _difficultyProp;
        private SerializedProperty _visualPaletteProp;
        private SerializedProperty _availableColorsProp;
        private SerializedProperty _boxPrefabProp;
        private SerializedProperty _wallPrefabProp;
        private SerializedProperty _cellSizeProp;
        private SerializedProperty _cellSpacingProp;
        private SerializedProperty _boardOriginProp;

        // Scene-only references (not saved in the SO)
        [SerializeField] private Transform _boardRoot;

        // Cached GUI styles (built lazily, first OnInspectorGUI call)
        private GUIStyle _cellLabelStyle;
        private GUIStyle _sectionHeaderStyle;
        private bool _stylesReady;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private void OnEnable()
        {
            _map = (GridMapDataSO)target;
            _map.EnsureCellListSize();

            _rowsProp = serializedObject.FindProperty("Rows");
            _colsProp = serializedObject.FindProperty("Cols");
            _mapNameProp = serializedObject.FindProperty("MapName");
            _difficultyProp = serializedObject.FindProperty("Difficulty");
            _visualPaletteProp = serializedObject.FindProperty("VisualPalette");
            _availableColorsProp = serializedObject.FindProperty("AvailableColors");
            _boxPrefabProp = serializedObject.FindProperty("BoxPrefab");
            _wallPrefabProp = serializedObject.FindProperty("WallPrefab");
            _cellSizeProp = serializedObject.FindProperty("CellSize");
            _cellSpacingProp = serializedObject.FindProperty("CellSpacing");
            _boardOriginProp = serializedObject.FindProperty("BoardOrigin");
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
            EditorGUILayout.PropertyField(_visualPaletteProp, new GUIContent("Box Palette"));
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

            if (_map.VisualPalette == null)
            {
                EditorGUILayout.HelpBox("Assign Box Palette to preview and build boxes with texture colors.", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            foreach (BoxColor color in _map.AvailableColors)
            {
                Texture texture = GetPaletteTexture(color);
                Rect rect = GUILayoutUtility.GetRect(42, 34, GUILayout.Width(42), GUILayout.Height(34));
                DrawPaletteRect(rect, color, texture);

                if (color == _selectedColor)
                {
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

            Texture selectedTexture = GetPaletteTexture(_selectedColor);
            Rect previewRect = GUILayoutUtility.GetRect(28, 24, GUILayout.Width(28), GUILayout.Height(24));
            DrawPaletteRect(previewRect, _selectedColor, selectedTexture);

            EditorGUILayout.LabelField($"Selected: {_selectedColor}", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Reset", GUILayout.Width(70)))
            {
                _selectedColor = _map.AvailableColors.Count > 0 ? _map.AvailableColors[0] : BoxColorUtility.DefaultColor;
            }
            EditorGUILayout.EndHorizontal();
        }

        private Texture GetPaletteTexture(BoxColor color)
        {
            if (_map == null || _map.VisualPalette == null)
            {
                return null;
            }

            return _map.VisualPalette.TryGetTexture(color, out Texture texture) ? texture : null;
        }

        private void DrawPaletteRect(Rect rect, BoxColor color, Texture texture)
        {
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f));
                GUI.Label(rect, color.ToString().Substring(0, 1), _cellLabelStyle);
            }
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
                        ApplyBoxVisual(instance, cell.Color);
                }
            }
        }

        private void ApplyBoxVisual(GameObject instance, BoxColor color)
        {
            if (instance == null || _map == null || _map.VisualPalette == null)
            {
                return;
            }

            if (!_map.VisualPalette.TryGetTexture(color, out Texture texture))
            {
                return;
            }

            Renderer renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            FlowBlast.Gameplay.Conveyor.BoxVisualView visualView = ConfigureBoxVisualView(instance, renderer);
            ConfigureBoxTapMover(instance, visualView);

            if (_map.VisualPalette.SharedMaterial != null)
            {
                renderer.sharedMaterial = _map.VisualPalette.SharedMaterial;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, texture);
            block.SetTexture(MainTexId, texture);
            renderer.SetPropertyBlock(block);
        }

        private FlowBlast.Gameplay.Conveyor.BoxVisualView ConfigureBoxVisualView(GameObject instance, Renderer renderer)
        {
            FlowBlast.Gameplay.Conveyor.BoxVisualView visualView = instance.GetComponent<FlowBlast.Gameplay.Conveyor.BoxVisualView>();
            if (visualView == null)
            {
                visualView = Undo.AddComponent<FlowBlast.Gameplay.Conveyor.BoxVisualView>(instance);
            }

            SerializedObject visualObject = new SerializedObject(visualView);
            SerializedProperty rendererProperty = visualObject.FindProperty("_renderer");
            SerializedProperty paletteProperty = visualObject.FindProperty("_palette");
            if (rendererProperty != null)
            {
                rendererProperty.objectReferenceValue = renderer;
            }
            if (paletteProperty != null)
            {
                paletteProperty.objectReferenceValue = _map.VisualPalette;
            }
            visualObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visualView);

            return visualView;
        }

        private void ConfigureBoxTapMover(GameObject instance, FlowBlast.Gameplay.Conveyor.BoxVisualView visualView)
        {
            FlowBlast.Gameplay.Conveyor.BoxTapMover tapMover = instance.GetComponent<FlowBlast.Gameplay.Conveyor.BoxTapMover>();
            if (tapMover == null)
            {
                return;
            }

            SerializedObject moverObject = new SerializedObject(tapMover);
            SerializedProperty visualViewProperty = moverObject.FindProperty("boxVisualView");
            SerializedProperty paletteProperty = moverObject.FindProperty("visualPalette");
            if (visualViewProperty != null)
            {
                visualViewProperty.objectReferenceValue = visualView;
            }
            if (paletteProperty != null)
            {
                paletteProperty.objectReferenceValue = _map.VisualPalette;
            }
            moverObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tapMover);
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

                ApplyBoxVisual(child.gameObject, cell.Color);
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

            if (cell.Type == CellType.Box)
            {
                Texture texture = GetPaletteTexture(cell.Color);
                if (texture != null)
                {
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
                }
            }

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
                _map.SetCell(r, c, new CellDataEntry(CellType.Exit, BoxColorUtility.DefaultColor));
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
                    return EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.82f, 0.82f, 0.82f);
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
                case CellType.Box: return GetPaletteTexture(cell.Color) == null ? "B" : "";
                default: return "";
            }
        }
    }
}