using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Lightweight tag component placed on every block spawned by SplineConveyor.
    /// Carries the BoxColor that the block should be matched against in the gameplay
    /// pipeline (GateMatcher, BoxContainer, etc.). Keeps conveyor prefabs agnostic of
    /// the project's color enum.
    /// </summary>
    public class ConveyorColoredBlock : MonoBehaviour
    {
        [SerializeField] private BoxColor color = BoxColor.Red;

        public BoxColor Color => color;

        public void SetColor(BoxColor newColor)
        {
            color = newColor;
        }
    }
}
