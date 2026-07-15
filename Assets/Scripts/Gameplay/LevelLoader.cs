using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay
{
    public class LevelLoader : MonoBehaviour
    {
        [Header("Subsystem References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private SplineConveyor _conveyor;
        [SerializeField] private BottomRayManager _bottomRayManager;
        [SerializeField] private GameProgressHUD _hud;

        [Header("Default Level")]
        [SerializeField] private GridMapDataSO _defaultLevel;

        private GridMapDataSO _currentConfig;

        private void Start()
        {
            if (_defaultLevel != null)
                LoadLevel(_defaultLevel);
        }

        public void LoadLevel(GridMapDataSO config)
        {
            if (config == null)
            {
                Debug.LogError("[LevelLoader] Level config is null!");
                return;
            }

            _currentConfig = config;

            if (_gridManager != null)
                _gridManager.LoadMapFromSO(config);
            else
                Debug.LogWarning("[LevelLoader] GridManager not assigned.");

            if (_conveyor != null)
                _conveyor.SetupFromConfig(config);
            else
                Debug.LogWarning("[LevelLoader] SplineConveyor not assigned.");

            if (_bottomRayManager != null)
                _bottomRayManager.SetupSlots(config.SlotCount, config.SlotCapacity);
            else
                Debug.LogWarning("[LevelLoader] BottomRayManager not assigned.");

            int target = config.TargetBoxCount > 0
                ? config.TargetBoxCount
                : config.GetTotalBoxCount();
            if (_hud != null)
                _hud.Bind(target);
            else
                Debug.LogWarning("[LevelLoader] GameProgressHUD not assigned.");

            Debug.Log($"[LevelLoader] Loaded level: {config.MapName} (target={target})");
        }

        public void ReloadLevel()
        {
            if (_currentConfig != null)
                LoadLevel(_currentConfig);
            else if (_defaultLevel != null)
                LoadLevel(_defaultLevel);
        }

        public GridMapDataSO CurrentConfig => _currentConfig;
    }
}
