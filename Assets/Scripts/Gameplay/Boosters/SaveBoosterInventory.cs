using FlowBlast.Managers;
using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class SaveBoosterInventory : IBoosterInventory
    {
        public int GetCount(BoosterType boosterType)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.Data == null)
            {
                return 0;
            }

            switch (boosterType)
            {
                case BoosterType.Shuffle:
                    return Mathf.Max(0, saveManager.Data.ShuffleBoosterCount);

                case BoosterType.Hand:
                    return Mathf.Max(0, saveManager.Data.HandBoosterCount);

                case BoosterType.Magnet:
                    return Mathf.Max(0, saveManager.Data.MagnetBoosterCount);

                default:
                    return 0;
            }
        }

        public bool CanSpend(BoosterType boosterType)
        {
            return GetCount(boosterType) > 0;
        }

        public bool TrySpend(BoosterType boosterType)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.Data == null || !CanSpend(boosterType))
            {
                return false;
            }

            switch (boosterType)
            {
                case BoosterType.Shuffle:
                    saveManager.Data.ShuffleBoosterCount = Mathf.Max(0, saveManager.Data.ShuffleBoosterCount - 1);
                    saveManager.Save();
                    return true;

                case BoosterType.Hand:
                    saveManager.Data.HandBoosterCount = Mathf.Max(0, saveManager.Data.HandBoosterCount - 1);
                    saveManager.Save();
                    return true;

                case BoosterType.Magnet:
                    saveManager.Data.MagnetBoosterCount = Mathf.Max(0, saveManager.Data.MagnetBoosterCount - 1);
                    saveManager.Save();
                    return true;

                default:
                    return false;
            }
        }
    }
}
