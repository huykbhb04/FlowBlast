namespace FlowBlast.Gameplay.Boosters
{
    public interface IShuffleRandomProvider
    {
        int Range(int minInclusive, int maxExclusive);
    }
}
