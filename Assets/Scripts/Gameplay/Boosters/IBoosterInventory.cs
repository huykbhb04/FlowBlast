namespace FlowBlast.Gameplay.Boosters
{
    public interface IBoosterInventory
    {
        int GetCount(BoosterType boosterType);
        bool CanSpend(BoosterType boosterType);
        bool TrySpend(BoosterType boosterType);
        void Add(BoosterType boosterType, int amount);
    }
}
