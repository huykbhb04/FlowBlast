using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class MagnetBooster : IBooster
    {
        private readonly GridManager _gridManager;

        public MagnetBooster(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public BoosterType Type => BoosterType.Magnet;

        public bool CanUse()
        {
            return _gridManager != null
                && _gridManager.GetGridMap() != null
                && _gridManager.CanActivateMagnetBooster();
        }

        public bool Use()
        {
            if (!CanUse())
            {
                return false;
            }

            _gridManager.ActivateMagnetBooster();
            return true;
        }
    }
}
