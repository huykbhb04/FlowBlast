using FlowBlast.Gameplay.Grid;
using FlowBlast.Managers;
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

        private void Awake()
        {
            if (_shuffleButton != null)
            {
                _shuffleButton.onClick.AddListener(UseShuffleBooster);
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
        }

        public bool UseBooster(BoosterType boosterType)
        {
            if (_gridManager == null)
            {
                Debug.LogWarning("[BoosterController] GridManager is not assigned.");
                return false;
            }

            bool used = _gridManager.UseBooster(boosterType);
            if (used && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(AudioId.BoosterActivate);
            }

            RefreshButtons();
            return used;
        }

        public void UseShuffleBooster()
        {
            UseBooster(BoosterType.Shuffle);
        }

        public void RefreshButtons()
        {
            if (_shuffleButton == null || _gridManager == null)
            {
                return;
            }

            _shuffleButton.interactable = _gridManager.CanUseBooster(BoosterType.Shuffle);
        }
    }
}
