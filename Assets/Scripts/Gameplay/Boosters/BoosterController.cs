using FlowBlast.Gameplay.Grid;
using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class BoosterController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private RuntimeBoardBuilder _boardBuilder;

        private readonly IBooster[] _boosters =
        {
            new ShuffleBoxColorsBooster()
        };

        private BoosterContext _context;

        private void Awake()
        {
            _context = new BoosterContext(_gridManager, _boardBuilder);
        }

        public bool CanUseShuffleBooster()
        {
            return CanUse(BoosterId.Shuffle);
        }

        public void UseShuffleBooster()
        {
            Use(BoosterId.Shuffle);
        }

        public bool CanUse(BoosterId boosterId)
        {
            IBooster booster = GetBooster(boosterId);
            return booster != null && booster.CanUse(_context);
        }

        public bool Use(BoosterId boosterId)
        {
            IBooster booster = GetBooster(boosterId);
            if (booster == null)
            {
                Debug.LogWarning($"[BoosterController] Booster '{boosterId}' is not registered.");
                return false;
            }

            bool wasUsed = booster.Use(_context);
            if (!wasUsed)
            {
                Debug.Log($"[BoosterController] Booster '{boosterId}' cannot be used now.");
                return false;
            }

            Debug.Log($"[BoosterController] Booster '{boosterId}' used.");
            return true;
        }

        private IBooster GetBooster(BoosterId boosterId)
        {
            for (int i = 0; i < _boosters.Length; i++)
            {
                IBooster booster = _boosters[i];
                if (booster != null && booster.Id == boosterId)
                {
                    return booster;
                }
            }

            return null;
        }
    }
}
