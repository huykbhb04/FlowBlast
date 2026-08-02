using System;
using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Holds the progress of a single Box that has been placed on the bottom ray.
    /// One Container per occupied slot.
    ///
    /// Flow:
    ///   1. Box assigned to slot -> Container.RequiredColor set, Progress = 0.
    ///   2. Each top block that matches RequiredColor at the gate adds ProgressStep (default 100).
    ///   3. When Progress >= 100 -> IsCompleted becomes true -> slot is freed.
    /// </summary>
    [Serializable]
    public class BoxContainer
    {
        [Tooltip("Color this container accepts. Set when a Box is assigned to the slot.")]
        public BoxColor RequiredColor;

        [Tooltip("Current fill progress 0..100. AddProgress() raises this value.")]
        [Range(0f, 100f)]
        public float Progress;

        [Tooltip("Amount added per matching top block at the gate. Default 5 (= 20 blocks to fill a slot; 10 blocks = 50%).")]
        public float ProgressStep = 5f;

        public bool IsCompleted => Progress >= 100f;

        public event Action<BoxContainer> OnProgressChanged;
        public event Action<BoxContainer> OnCompleted;

        public BoxContainer()
        {
            RequiredColor = BoxColorUtility.DefaultColor;
            Progress = 0f;
            ProgressStep = 5f;
        }

        public BoxContainer(BoxColor requiredColor, float progressStep = 5f)
        {
            RequiredColor = requiredColor;
            Progress = 0f;
            ProgressStep = progressStep;
        }

        public void Reset(BoxColor requiredColor)
        {
            RequiredColor = requiredColor;
            Progress = 0f;
        }

        public void AddProgress(float amount)
        {
            if (IsCompleted) return;
            Progress = Mathf.Clamp(Progress + amount, 0f, 100f);
            OnProgressChanged?.Invoke(this);
            if (IsCompleted)
                OnCompleted?.Invoke(this);
        }
    }
}