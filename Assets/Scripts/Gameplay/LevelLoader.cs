using UnityEngine;
using FlowBlast.Core;
using FlowBlast.Gameplay.Grid;
using FlowBlast.Gameplay.Conveyor;
using FlowBlast.Managers;

namespace FlowBlast.Gameplay
{
    /// <summary>
    /// Orchestrates loading a level: wires up GridManager, SplineConveyor,
    /// BottomRayManager, HUD, and triggers. Integrates with SaveManager
    /// to track the current level index.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [Header("Subsystem References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private SplineConveyor _conveyor;
        [SerializeField] private BottomRayManager _bottomRayManager;
        [SerializeField] private GameProgressHUD _hud;

        [Header("Triggers")]
        [SerializeField] private WinTrigger _winTrigger;
        [SerializeField] private LoseTrigger _loseTrigger;

        [Header("Default Level")]
        [SerializeField] private GridMapDataSO _defaultLevel;

        [Header("All Levels (ordered)")]
        [Tooltip("Drag all level SOs here in order. Used by NextLevel().")]
        [SerializeField] private GridMapDataSO[] _allLevels;

        private GridMapDataSO _currentConfig;

        private void Start()
        {
            // Pick the level from SaveManager if available.
            GridMapDataSO levelToLoad = _defaultLevel;

            if (SaveManager.Instance != null && _allLevels != null && _allLevels.Length > 0)
            {
                int idx = SaveManager.Instance.Data.CurrentLevel;
                if (idx >= 0 && idx < _allLevels.Length)
                    levelToLoad = _allLevels[idx];
            }

            if (levelToLoad != null)
                LoadLevel(levelToLoad);
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

            // Reset triggers so they can fire for this new level.
            if (_winTrigger != null) _winTrigger.ResetTrigger();
            if (_loseTrigger != null) _loseTrigger.ResetTrigger();

            Debug.Log($"[LevelLoader] Loaded level: {config.MapName} (index={config.LevelIndex}, target={target})");
        }

        public void ReloadLevel()
        {
            if (_currentConfig != null)
                LoadLevel(_currentConfig);
            else if (_defaultLevel != null)
                LoadLevel(_defaultLevel);
        }

        /// <summary>
        /// Load the next level in the _allLevels array.
        /// Returns false if already at the last level.
        /// </summary>
        public bool LoadNextLevel()
        {
            if (_allLevels == null || _allLevels.Length == 0) return false;

            int currentIdx = _currentConfig != null ? _currentConfig.LevelIndex : 0;
            int nextIdx = currentIdx + 1;
            if (nextIdx >= _allLevels.Length) return false;

            LoadLevel(_allLevels[nextIdx]);
            return true;
        }

        public GridMapDataSO CurrentConfig => _currentConfig;
    }
}
