using UnityEngine;
using FlowBlast.Data;

namespace FlowBlast.Managers
{
    /// <summary>
    /// Singleton responsible for persisting and loading <see cref="SaveData"/>
    /// as JSON in <c>Application.persistentDataPath/flowblast_save.json</c>.
    ///
    /// Auto-loads on Awake. Call <see cref="Save"/> after any change you want persisted.
    ///
    /// Example:
    ///   SaveManager.Instance.Data.Coins += 100;
    ///   SaveManager.Instance.Save();
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string FILE_NAME = "flowblast_save.json";

        [Header("Debug")]
        [SerializeField] private bool _logIO = true;

        private SaveData _data;
        private string _filePath;

        /// <summary>Current in-memory save data. Never null after Awake.</summary>
        public SaveData Data => _data;

        // ─── Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _filePath = System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME);
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Auto-save when the app goes to background (mobile).
            if (pauseStatus) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Write current <see cref="Data"/> to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(_data, prettyPrint: true);
                System.IO.File.WriteAllText(_filePath, json);
                if (_logIO) Debug.Log($"[SaveManager] Saved to {_filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load data from disk. Creates fresh <see cref="SaveData"/> if file doesn't exist.
        /// </summary>
        public void Load()
        {
            if (System.IO.File.Exists(_filePath))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(_filePath);
                    _data = JsonUtility.FromJson<SaveData>(json);
                    if (_logIO) Debug.Log($"[SaveManager] Loaded from {_filePath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SaveManager] Load failed: {ex.Message}. Creating fresh save.");
                    _data = new SaveData();
                }
            }
            else
            {
                _data = new SaveData();
                if (_logIO) Debug.Log("[SaveManager] No save file found — starting fresh.");
            }
        }

        /// <summary>
        /// Delete saved file and reset to defaults.
        /// </summary>
        public void DeleteSave()
        {
            if (System.IO.File.Exists(_filePath))
                System.IO.File.Delete(_filePath);
            _data = new SaveData();
            if (_logIO) Debug.Log("[SaveManager] Save data deleted.");
        }

        // ─── Economy Helpers ────────────────────────────────────────────

        /// <summary>Add coins and auto-save.</summary>
        public void AddCoins(int amount)
        {
            _data.Coins += amount;
            if (_data.Coins < 0) _data.Coins = 0;
            Save();
        }

        /// <summary>Spend coins if balance is sufficient. Returns true on success.</summary>
        public bool SpendCoins(int cost)
        {
            if (_data.Coins < cost) return false;
            _data.Coins -= cost;
            Save();
            return true;
        }

        /// <summary>Check if the player can afford a cost.</summary>
        public bool CanAfford(int cost) => _data.Coins >= cost;
    }
}
