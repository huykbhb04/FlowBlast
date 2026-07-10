using UnityEditor;
using UnityEngine;
using System.IO;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// Auto-create clean Prefab assets for the Grid Map Editor by wrapping .fbx files.
    /// Removes any "spline-driven" components that don't belong on per-cell prefabs.
    /// </summary>
    public static class PrefabAutoCreator
    {
        private const string ModelFolder = "Assets/Art/Model";
        private const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Tools/FlowBlast/Auto-Create Clean Prefabs From Models")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            CreateFromModel("Obstacle_Ice.fbx", "WallPrefab.prefab",
                removeSpline: true, removeBottomConveyor: true, removeBoxTapMover: true);

            CreateFromModel("conveyor_box.fbx", "BoxPrefab.prefab",
                removeSpline: true, removeBottomConveyor: true, removeBoxTapMover: false);

            CreateFromModel("counter.fbx", "ExitPrefab.prefab",
                removeSpline: true, removeBottomConveyor: true, removeBoxTapMover: true);

            CreateFromModel("Slot.fbx", "SlotPrefab.prefab",
                removeSpline: true, removeBottomConveyor: true, removeBoxTapMover: true);

            CreateFromModel("Background.fbx", "BackgroundPrefab.prefab",
                removeSpline: true, removeBottomConveyor: true, removeBoxTapMover: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FlowBlast] Clean prefabs created in {PrefabFolder}/");
        }

        private static void CreateFromModel(string fbxName, string prefabName,
            bool removeSpline, bool removeBottomConveyor, bool removeBoxTapMover)
        {
            string fbxPath = $"{ModelFolder}/{fbxName}";
            string prefabPath = $"{PrefabFolder}/{prefabName}";

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[FlowBlast] FBX not found: {fbxPath}");
                return;
            }

            var tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            tempInstance.name = Path.GetFileNameWithoutExtension(prefabName);

            StripComponents(tempInstance, removeSpline, removeBottomConveyor, removeBoxTapMover);

            PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
            Object.DestroyImmediate(tempInstance);

            Debug.Log($"[FlowBlast] Created: {prefabPath}");
        }

        private static void StripComponents(GameObject go,
            bool removeSpline, bool removeBottomConveyor, bool removeBoxTapMover)
        {
            var all = go.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().FullName ?? string.Empty;

                if (removeSpline && typeName.Contains("SplineFollower"))
                {
                    Object.DestroyImmediate(mb, true);
                    continue;
                }
                if (removeBottomConveyor && typeName.Contains("BottomConveyorController"))
                {
                    Object.DestroyImmediate(mb, true);
                    continue;
                }
                if (removeBoxTapMover && typeName.Contains("BoxTapMover"))
                {
                    Object.DestroyImmediate(mb, true);
                }
            }
        }
    }
}