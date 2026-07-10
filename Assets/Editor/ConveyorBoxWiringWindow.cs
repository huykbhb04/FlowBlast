using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using FlowBlast.Gameplay.Conveyor;

namespace FlowBlast.Gameplay.Grid.EditorTools
{
    /// <summary>
    /// One-shot wizard: pick a SplineContainer + ReceivePoint transform, then
    /// bake those references into every BoxTapMover found under a chosen board root.
    ///
    /// Why this exists: BuildBoard instantiates Box prefabs but does not (and should not)
    /// carry scene references around inside the SO. After clicking Build Board you have
    /// a pile of Box_* children in Hierarchy with empty SplineContainer / ReceivePoint
    /// fields. This wizard walks that hierarchy and wires them up.
    ///
    /// Usage:
    ///   Tools > FlowBlast > Wire Conveyor Box References
    /// Or use the "Wire Boxes" button in GridMapDataSOEditor.
    /// </summary>
    public class ConveyorBoxWiringWindow : EditorWindow
    {
        // Edits live in scene, but we persist last-used IDs so they auto-populate next time.
        private const string PrefSplineGuid = "FlowBlast.Wiring.SplineGuid";
        private const string PrefReceiveGuid = "FlowBlast.Wiring.ReceiveGuid";
        private const string PrefBoardName = "FlowBlast.Wiring.BoardName";

        private SplineContainer _spline;
        private Transform _receivePoint;
        private string _boardRootName = "Ob";

        [MenuItem("Tools/FlowBlast/Wire Conveyor Box References")]
        public static void Open()
        {
            var w = GetWindow<ConveyorBoxWiringWindow>(true, "Wire Conveyor Boxes", true);
            w.minSize = new Vector2(380, 180);
            w.Show();
        }

        private void OnEnable()
        {
            // Pre-populate last session's choices so power users don't redo them.
            _spline = LoadByGlobalObjectId<SplineContainer>(EditorPrefs.GetString(PrefSplineGuid, ""));
            string recvId = EditorPrefs.GetString(PrefReceiveGuid, "");
            if (!string.IsNullOrEmpty(recvId))
            {
                var go = LoadGlobalObjectById(recvId);
                if (go != null) _receivePoint = go.transform;
            }
            _boardRootName = EditorPrefs.GetString(PrefBoardName, _boardRootName);
        }

        public static void OpenWith(SplineContainer spline, Transform receivePoint, string boardRootName)
        {
            Open();
            var ws = Resources.FindObjectsOfTypeAll<ConveyorBoxWiringWindow>();
            if (ws.Length == 0) return;
            var w = ws[0];
            w._spline = spline;
            w._receivePoint = receivePoint;
            w._boardRootName = boardRootName;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wire Conveyor Box References", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) Drag a SplineContainer (the conveyor spline in your scene).\n" +
                "2) Drag the empty ReceivePoint transform boxes should jump to.\n" +
                "3) Optionally change the board-root GameObject name (default 'Ob').\n" +
                "4) Click 'Wire Boxes'. Every Box_* child underneath gets its BoxTapMover references set.",
                MessageType.Info);

            _spline = (SplineContainer)EditorGUILayout.ObjectField("Spline Container", _spline, typeof(SplineContainer), true);
            _receivePoint = (Transform)EditorGUILayout.ObjectField("Receive Point", _receivePoint, typeof(Transform), true);
            _boardRootName = EditorGUILayout.TextField("Board Root Name", _boardRootName);

            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(_spline == null || _receivePoint == null || string.IsNullOrEmpty(_boardRootName)))
            {
                if (GUILayout.Button("Wire Boxes", GUILayout.Height(28)))
                {
                    if (TryPersist())
                        WireBoxes(_spline, _receivePoint, _boardRootName);
                }
            }

            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(_spline == null || _receivePoint == null))
            {
                if (GUILayout.Button("Wire Boxes (find board root automatically)"))
                {
                    var root = FindBoardRootAuto();
                    if (root == null)
                    {
                        EditorUtility.DisplayDialog("FlowBlast",
                            "Could not find a board root automatically. Looking for GameObjects containing Box_ children. " +
                            "Set a board root name manually.", "OK");
                        return;
                    }

                    if (TryPersist())
                        WireBoxes(_spline, _receivePoint, root.name);
                }
            }
        }

        private bool TryPersist()
        {
            if (_spline != null) EditorPrefs.SetString(PrefSplineGuid, GlobalObjectIdToId(_spline));
            if (_receivePoint != null) EditorPrefs.SetString(PrefReceiveGuid, GlobalObjectIdToId(_receivePoint));
            EditorPrefs.SetString(PrefBoardName, _boardRootName ?? "");
            return true;
        }

        private GameObject FindBoardRootAuto()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (t.parent != null) continue;
                int boxCount = 0;
                foreach (Transform _ in t) boxCount++;
                if (boxCount == 0) continue;
                foreach (Transform child in t)
                {
                    if (child != null && child.name.StartsWith("Box_"))
                        return t.gameObject;
                }
            }
            return null;
        }

        public static void WireBoxes(SplineContainer spline, Transform receivePoint, string boardRootName)
        {
            if (spline == null || receivePoint == null || string.IsNullOrEmpty(boardRootName))
            {
                Debug.LogWarning("[FlowBlast] WireBoxes: missing inputs.");
                return;
            }

            GameObject root = GameObject.Find(boardRootName);
            if (root == null)
            {
                Debug.LogWarning($"[FlowBlast] WireBoxes: no GameObject named '{boardRootName}' in active scene.");
                return;
            }

            int touched = 0;
            var movers = new List<BoxTapMover>();
            root.GetComponentsInChildren(true, movers);

            foreach (var m in movers)
            {
                if (m == null) continue;
                Undo.RecordObject(m, "Wire Box Conveyor Refs");
                m.SetSplineReference(spline);
                m.SetReceivePoint(receivePoint);
                EditorUtility.SetDirty(m);
                touched++;
            }

            Debug.Log($"[FlowBlast] Wired SplineContainer + ReceivePoint into {touched} BoxTapMover(s) under '{boardRootName}'.");
        }

        // ------------------------------------------------------------------
        // Small helpers for storing scene-object references in EditorPrefs.
        // ------------------------------------------------------------------

        private static string GlobalObjectIdToId(Object obj)
        {
            if (obj == null) return string.Empty;
            string guid;
            long fileId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out fileId))
                return $"{guid}|{fileId}";
            return string.Empty;
        }

        private static T LoadByGlobalObjectId<T>(string id) where T : Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            string[] parts = id.Split('|');
            if (parts.Length != 2) return null;
            string path = AssetDatabase.GUIDToAssetPath(parts[0]);
            if (string.IsNullOrEmpty(path)) return null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in assets)
            {
                if (a is T t) return t;
            }
            return null;
        }

        private static GameObject LoadGlobalObjectById(string id)
        {
            string[] parts = id.Split('|');
            if (parts.Length != 2) return null;
            string path = AssetDatabase.GUIDToAssetPath(parts[0]);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
