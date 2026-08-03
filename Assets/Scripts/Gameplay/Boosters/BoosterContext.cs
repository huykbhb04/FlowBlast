using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class BoosterContext
    {
        public BoosterContext(GridManager gridManager, IBoardBoxRegistry boardBoxRegistry)
        {
            GridManager = gridManager;
            BoardBoxRegistry = boardBoxRegistry;
        }

        public GridManager GridManager { get; }
        public IBoardBoxRegistry BoardBoxRegistry { get; }
    }
}
