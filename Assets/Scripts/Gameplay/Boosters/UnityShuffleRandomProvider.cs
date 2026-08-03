using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class UnityShuffleRandomProvider : IShuffleRandomProvider
    {
        public int Range(int minInclusive, int maxExclusive)
        {
            return Random.Range(minInclusive, maxExclusive);
        }
    }
}
