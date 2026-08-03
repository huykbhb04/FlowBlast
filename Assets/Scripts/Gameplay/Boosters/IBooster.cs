namespace FlowBlast.Gameplay.Boosters
{
    public interface IBooster
    {
        BoosterType Type { get; }
        bool CanUse();
        bool Use();
    }
}
