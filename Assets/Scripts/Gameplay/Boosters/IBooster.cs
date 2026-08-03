namespace FlowBlast.Gameplay.Boosters
{
    public interface IBooster
    {
        BoosterId Id { get; }
        bool CanUse(BoosterContext context);
        bool Use(BoosterContext context);
    }
}
