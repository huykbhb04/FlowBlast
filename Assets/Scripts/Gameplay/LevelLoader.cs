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
        public static LevelLoader Instance { get; private set; }

        [Header("Subsystem References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private RuntimeBoardBuilder _boardBuilder;
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

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            // Pick the level from SaveManager if available.
            GridMapDataSO levelToLoad = _defaultLevel;

            if (SaveManager.Instance != null && _allLevels != null && _allLevels.Length > 0)
            {
                int index = Mathf.Clamp(SaveManager.Instance.Data.CurrentLevel, 0, _allLevels.Length - 1);
                SaveManager.Instance.Data.CurrentLevel = index;
                levelToLoad = _allLevels[index];
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
            {
                _gridManager.LoadMapFromSO(config);
            }
            else
            {
                Debug.LogWarning("[LevelLoader] GridManager not assigned.");
            }

            if (_boardBuilder != null)
            {
                _boardBuilder.Build(config);
            }
            else
            {
                Debug.LogWarning("[LevelLoader] RuntimeBoardBuilder not assigned. Board will not be generated from GridMapDataSO.");
            }

            if (_conveyor != null)
                _conveyor.SetupFromConfig(config);
            else
                Debug.LogWarning("[LevelLoader] SplineConveyor not assigned.");

            if (_bottomRayManager != null)
            {
                if (_gridManager != null)
                {
                    _bottomRayManager.SetSlotSpline(_gridManager.BottomSpline, _gridManager.BottomSplineIndex);
                    _bottomRayManager.SetMovement(_gridManager.MoveSpeed, _gridManager.LoopSpline);
                }

                _bottomRayManager.SetupSlots(config.SlotCount, config.SlotCapacity);
            }
            else
            {
                Debug.LogWarning("[LevelLoader] BottomRayManager not assigned.");
            }

            int target = config.TargetBoxCount > 0
                ? config.TargetBoxCount
                : config.GetTotalBoxCount();
            if (_hud != null)
                _hud.Bind(target);
            else
                Debug.LogWarning("[LevelLoader] GameProgressHUD not assigned.");

            if (_winTrigger != null)
            {
                _winTrigger.Initialize(_gridManager, _bottomRayManager, this);
                _winTrigger.ResetTrigger();
            }

            if (_loseTrigger != null)
            {
                _loseTrigger.Initialize(_bottomRayManager, _conveyor);
                _loseTrigger.ResetTrigger();
            }

            Debug.Log($"[LevelLoader] Loaded level: {config.MapName} (arrayIndex={GetCurrentLevelIndex()}, assetIndex={config.LevelIndex}, target={target})");
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
            if (_allLevels == null || _allLevels.Length == 0)
            {
                return false;
            }

            int currentIndex = GetCurrentLevelIndex();
            int nextIndex = currentIndex + 1;
            if (nextIndex >= _allLevels.Length)
            {
                return false;
            }

            LoadLevel(_allLevels[nextIndex]);
            return true;
        }

        public bool AdvanceToNextLevel()
        {
            if (_allLevels == null || _allLevels.Length == 0)
            {
                return false;
            }

            int currentIndex = GetCurrentLevelIndex();
            int nextIndex = currentIndex + 1;
            if (nextIndex >= _allLevels.Length)
            {
                return false;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                saveManager.Data.CurrentLevel = nextIndex;
                if (saveManager.Data.HighestUnlockedLevel < nextIndex)
                {
                    saveManager.Data.HighestUnlockedLevel = nextIndex;
                }

                saveManager.Save();
            }

            LoadLevel(_allLevels[nextIndex]);
            return true;
        }

        private int GetCurrentLevelIndex()
        {
            int currentArrayIndex = GetCurrentLevelArrayIndex();
            if (currentArrayIndex >= 0)
            {
                return currentArrayIndex;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                return Mathf.Clamp(saveManager.Data.CurrentLevel, 0, GetLastLevelIndex());
            }

            return 0;
        }

        private int GetCurrentLevelArrayIndex()
        {
            if (_currentConfig == null || _allLevels == null)
            {
                return -1;
            }

            for (int i = 0; i < _allLevels.Length; i++)
            {
                if (_allLevels[i] == _currentConfig)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetLastLevelIndex()
        {
            if (_allLevels == null || _allLevels.Length == 0)
            {
                return 0;
            }

            return _allLevels.Length - 1;
        }

        public GridMapDataSO CurrentConfig => _currentConfig;
    }
}
