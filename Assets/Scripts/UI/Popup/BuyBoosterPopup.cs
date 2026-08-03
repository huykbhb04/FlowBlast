using System;
using FlowBlast.Gameplay.Boosters;
using FlowBlast.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    public sealed class BuyBoosterPopup : BasePopup
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _buyButton;

        [Header("Display")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private TextMeshProUGUI _costLabel;
        [SerializeField] private string _costFormat = "{0}";

        private BoosterPurchaseData _purchaseData;
        private IBoosterInventory _boosterInventory;
        private Action<BoosterType> _onPurchased;
        private bool _isInitialized;

        protected override void Awake()
        {
            base.Awake();

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_buyButton != null)
            {
                _buyButton.onClick.AddListener(BuyBooster);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }

            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(BuyBooster);
            }
        }

        public void Initialize(BoosterPurchaseData purchaseData, IBoosterInventory boosterInventory, Action<BoosterType> onPurchased)
        {
            _purchaseData = purchaseData;
            _boosterInventory = boosterInventory;
            _onPurchased = onPurchased;
            _isInitialized = true;

            RefreshView();
        }

        public override void OnShown()
        {
            base.OnShown();
            RefreshView();
        }

        private void RefreshView()
        {
            if (!_isInitialized)
            {
                return;
            }

            SetIcon();
            SetText(_titleLabel, _purchaseData.DisplayName);
            SetText(_descriptionLabel, _purchaseData.Description);
            SetText(_costLabel, string.Format(_costFormat, _purchaseData.Cost));

            if (_buyButton != null)
            {
                SaveManager saveManager = SaveManager.Instance;
                _buyButton.interactable = saveManager != null && saveManager.CanAfford(_purchaseData.Cost);
            }
        }

        private void SetIcon()
        {
            if (_iconImage == null)
            {
                return;
            }

            _iconImage.sprite = _purchaseData.Icon;
            _iconImage.enabled = _purchaseData.Icon != null;
            _iconImage.preserveAspect = true;
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label == null)
            {
                return;
            }

            label.text = value;
        }

        private void BuyBooster()
        {
            if (!_isInitialized || _boosterInventory == null)
            {
                return;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || !saveManager.SpendCoins(_purchaseData.Cost))
            {
                RefreshView();
                return;
            }

            _boosterInventory.Add(_purchaseData.Type, _purchaseData.Amount);
            _onPurchased?.Invoke(_purchaseData.Type);
            Close();
        }
    }
}
