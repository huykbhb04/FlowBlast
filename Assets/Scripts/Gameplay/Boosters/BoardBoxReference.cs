using FlowBlast.Gameplay.Conveyor;

namespace FlowBlast.Gameplay.Boosters
{
    public readonly struct BoardBoxReference
    {
        public BoardBoxReference(int row, int col, BoxTapMover mover)
        {
            Row = row;
            Col = col;
            Mover = mover;
        }

        public int Row { get; }
        public int Col { get; }
        public BoxTapMover Mover { get; }
        public bool IsValid => Mover != null && Mover.IsActiveOnGrid;
    }
}
