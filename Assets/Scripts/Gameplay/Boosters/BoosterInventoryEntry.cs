using System;
using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    [Serializable]
    public struct BoosterInventoryEntry
    {
        public BoosterType Type;
        [Min(0)] public int DefaultCount;

        public BoosterInventoryEntry(BoosterType type, int defaultCount)
        {
            Type = type;
            DefaultCount = defaultCount;
        }
    }
}
