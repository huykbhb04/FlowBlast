using System.Collections.Generic;

namespace FlowBlast.Gameplay.Boosters
{
    public interface IBoardBoxRegistry
    {
        int ActiveBoxCount { get; }
        IReadOnlyList<BoardBoxReference> ActiveBoxes { get; }
    }
}
