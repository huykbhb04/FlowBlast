using FlowBlast.Gameplay.Grid;
using FlowBlast.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class BoosterController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;

        [Header("Buttons")]
        [SerializeField] private Button _shuffleButton;
        [SerializeField] private Button _handButton;

        [Header("Shuffle Count Display")]
        [SerializeField] private TextMeshProUGUI _shuffleCountTmpLabel;
        [SerializeField] private string _shuffleCountFormat = "{0}";

        [Header("Hand Count Display")]
        [SerializeField] private TextMeshProUGUI _handCountTmpLabel;
        [SerializeField] private string _handCountFormat = "{0}";

        private readonly IBoosterInventory _boosterInventory = new SaveBoosterInventory();

        private void Awake()
        {
            if (_shuffleButton != null)
            {
                _shuffleButton.onClick.AddListener(UseShuffleBooster);
            }

            if (_handButton != null)
            {
                _handButton.onClick.AddListener(UseHandBooster);
            }
        }

        private void OnEnable()
        {
            if (_gridManager != null)
            {
                _gridManager.OnBoardStateChanged += RefreshButtons;
            }

            RefreshButtons();
        }

        private void OnDisable()
        {
            if (_gridManager != null)
            {
                _gridManager.OnBoardStateChanged -= RefreshButtons;
            }
        }

        private void OnDestroy()
        {
            if (_shuffleButton != null)
            {
                _shuffleButton.onClick.RemoveListener(UseShuffleBooster);
            }

            if (_handButton != null)
            {
                _handButton.onClick.RemoveListener(UseHandBooster);
            }
        }

        public bool UseBooster(BoosterType boosterType)
        {
            if (_gridManager == null)
            {
                Debug.LogWarning("[BoosterController] GridManager is not assigned.");
                return false;
            }

            if (!_boosterInventory.CanSpend(boosterType))
            {
                Debug.Log($"[BoosterController] Booster '{boosterType}' count is empty.");
                RefreshButtons();
                return false;
            }

            bool used = _gridManager.UseBooster(boosterType);
            if (!used)
            {
                RefreshButtons();
                return false;
            }

            if (!_boosterInventory.TrySpend(boosterType))
            {
                Debug.LogWarning($"[BoosterController] Failed to spend booster '{boosterType}' after use.");
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(AudioId.BoosterActivate);
            }

            RefreshButtons();
            return true;
        }

        public void UseShuffleBooster()
        {
            UseBooster(BoosterType.Shuffle);
        }

        public void UseHandBooster()
        {
            UseBooster(BoosterType.Hand);
        }

        public void RefreshButtons()
        {
            RefreshShuffleButton();
            RefreshHandButton();
            RefreshShuffleCount();
            RefreshHandCount();
        }

        private void RefreshShuffleButton()
        {
            if (_shuffleButton == null || _gridManager == null)
            {
                return;
            }

            _shuffleButton.interactable = _gridManager.CanUseBooster(BoosterType.Shuffle)
                && _boosterInventory.CanSpend(BoosterType.Shuffle);
        }

        private void RefreshHandButton()
        {
            if (_handButton == null || _gridManager == null)
            {
                return;
            }

            _handButton.interactable = _gridManager.CanUseBooster(BoosterType.Hand)
                && _boosterInventory.CanSpend(BoosterType.Hand);
        }

        private void RefreshShuffleCount()
        {
            SetCountText(_shuffleCountTmpLabel, _shuffleCountFormat, BoosterType.Shuffle);
        }

        private void RefreshHandCount()
        {
            SetCountText(_handCountTmpLabel, _handCountFormat, BoosterType.Hand);
        }

        private void SetCountText(TextMeshProUGUI label, string format, BoosterType boosterType)
        {
            if (label == null)
            {
                return;
            }

            int count = _boosterInventory.GetCount(boosterType);
            label.text = string.Format(format, count);
        }
    }
}
