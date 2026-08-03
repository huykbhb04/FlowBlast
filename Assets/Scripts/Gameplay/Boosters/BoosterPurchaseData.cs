using System;
using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    [Serializable]
    public struct BoosterPurchaseData
    {
        public BoosterType Type;
        [Min(1)] public int Amount;
        [Min(0)] public int Cost;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
    }
}
