using FlowBlast.Gameplay.Grid;
using FlowBlast.Managers;
using FlowBlast.UI;
using FlowBlast.UI.Popup;
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
        [SerializeField] private Button _magnetButton;

        [Header("Shuffle Count Display")]
        [SerializeField] private TextMeshProUGUI _shuffleCountTmpLabel;
        [SerializeField] private string _shuffleCountFormat = "{0}";

        [Header("Hand Count Display")]
        [SerializeField] private TextMeshProUGUI _handCountTmpLabel;
        [SerializeField] private string _handCountFormat = "{0}";

        [Header("Magnet Count Display")]
        [SerializeField] private TextMeshProUGUI _magnetCountTmpLabel;
        [SerializeField] private string _magnetCountFormat = "{0}";

        [Header("Shop")]
        [SerializeField] private BoosterPurchaseData _shufflePurchaseData = new BoosterPurchaseData
        {
            Type = BoosterType.Shuffle,
            Amount = 1,
            Cost = 100,
            DisplayName = "Shuffle Booster",
            Description = "Shuffle all boxes on the board."
        };
        [SerializeField] private BoosterPurchaseData _handPurchaseData = new BoosterPurchaseData
        {
            Type = BoosterType.Hand,
            Amount = 1,
            Cost = 100,
            DisplayName = "Hand Booster",
            Description = "Move one selected box manually."
        };
        [SerializeField] private BoosterPurchaseData _magnetPurchaseData = new BoosterPurchaseData
        {
            Type = BoosterType.Magnet,
            Amount = 1,
            Cost = 100,
            DisplayName = "Magnet Booster",
            Description = "Pull matching boxes with magnet power."
        };
        [SerializeField] private string _emptyCountText = "+";

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

            if (_magnetButton != null)
            {
                _magnetButton.onClick.AddListener(UseMagnetBooster);
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

            if (_magnetButton != null)
            {
                _magnetButton.onClick.RemoveListener(UseMagnetBooster);
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
            UseOrBuyBooster(BoosterType.Shuffle);
        }

        public void UseHandBooster()
        {
            UseOrBuyBooster(BoosterType.Hand);
        }

        public void UseMagnetBooster()
        {
            UseOrBuyBooster(BoosterType.Magnet);
        }

        private void UseOrBuyBooster(BoosterType boosterType)
        {
            if (_boosterInventory.CanSpend(boosterType))
            {
                UseBooster(boosterType);
                return;
            }

            OpenBuyBoosterPopup(boosterType);
        }

        public void RefreshButtons()
        {
            RefreshShuffleButton();
            RefreshHandButton();
            RefreshMagnetButton();
            RefreshShuffleCount();
            RefreshHandCount();
            RefreshMagnetCount();
        }

        private void RefreshShuffleButton()
        {
            RefreshButton(_shuffleButton, BoosterType.Shuffle);
        }

        private void RefreshHandButton()
        {
            RefreshButton(_handButton, BoosterType.Hand);
        }

        private void RefreshMagnetButton()
        {
            RefreshButton(_magnetButton, BoosterType.Magnet);
        }

        private void RefreshButton(Button button, BoosterType boosterType)
        {
            if (button == null)
            {
                return;
            }

            if (!_boosterInventory.CanSpend(boosterType))
            {
                button.interactable = true;
                return;
            }

            button.interactable = _gridManager != null && _gridManager.CanUseBooster(boosterType);
        }

        private void RefreshShuffleCount()
        {
            SetCountText(_shuffleCountTmpLabel, _shuffleCountFormat, BoosterType.Shuffle);
        }

        private void RefreshHandCount()
        {
            SetCountText(_handCountTmpLabel, _handCountFormat, BoosterType.Hand);
        }

        private void RefreshMagnetCount()
        {
            SetCountText(_magnetCountTmpLabel, _magnetCountFormat, BoosterType.Magnet);
        }

        private void SetCountText(TextMeshProUGUI label, string format, BoosterType boosterType)
        {
            if (label == null)
            {
                return;
            }

            int count = _boosterInventory.GetCount(boosterType);
            label.text = count > 0 ? string.Format(format, count) : _emptyCountText;
        }

        private void OpenBuyBoosterPopup(BoosterType boosterType)
        {
            PopupManager popupManager = PopupManager.Instance;
            if (popupManager == null)
            {
                Debug.LogWarning("[BoosterController] PopupManager is not available.");
                return;
            }

            BasePopup popup = popupManager.Show(PopupId.BuyBooster);
            BuyBoosterPopup buyBoosterPopup = popup as BuyBoosterPopup;
            if (buyBoosterPopup == null)
            {
                Debug.LogWarning("[BoosterController] BuyBoosterPopup prefab is not registered or missing BuyBoosterPopup component.");
                return;
            }

            buyBoosterPopup.Initialize(GetPurchaseData(boosterType), _boosterInventory, HandleBoosterPurchased);
        }

        private BoosterPurchaseData GetPurchaseData(BoosterType boosterType)
        {
            switch (boosterType)
            {
                case BoosterType.Shuffle:
                    return _shufflePurchaseData;

                case BoosterType.Hand:
                    return _handPurchaseData;

                case BoosterType.Magnet:
                    return _magnetPurchaseData;

                default:
                    return _shufflePurchaseData;
            }
        }

        private void HandleBoosterPurchased(BoosterType boosterType)
        {
            RefreshButtons();
            HUDController hudController = HUDController.Instance;
            if (hudController != null)
            {
                hudController.RefreshCoinDisplay();
            }
        }
    }
}
