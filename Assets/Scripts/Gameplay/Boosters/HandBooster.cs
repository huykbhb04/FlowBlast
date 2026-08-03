using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class HandBooster : IBooster
    {
        private readonly GridManager _gridManager;

        public HandBooster(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public BoosterType Type => BoosterType.Hand;

        public bool CanUse()
        {
            return _gridManager != null
                && _gridManager.GetGridMap() != null
                && _gridManager.HasAnyBoxOnGrid()
                && !_gridManager.IsHandBoosterActive;
        }

        public bool Use()
        {
            if (!CanUse())
            {
                return false;
            }

            _gridManager.ActivateHandBooster();
            return true;
        }
    }
}
