using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    [CreateAssetMenu(fileName = "BoosterInventoryConfig", menuName = "FlowBlast/Boosters/Inventory Config", order = 1)]
    public sealed class BoosterInventoryConfigSO : ScriptableObject
    {
        [SerializeField] private BoosterInventoryEntry[] _entries =
        {
            new BoosterInventoryEntry(BoosterType.Shuffle, 3),
            new BoosterInventoryEntry(BoosterType.Hand, 3)
        };

        public int GetDefaultCount(BoosterType boosterType)
        {
            if (_entries == null)
            {
                return 0;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Type == boosterType)
                {
                    return Mathf.Max(0, _entries[i].DefaultCount);
                }
            }

            return 0;
        }
    }
}
