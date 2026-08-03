using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI
{
    /// <summary>
    /// UI-grid layout helpers.
    ///
    /// <para>
    /// Use these when you need a multi-row, multi-column arrangement INSIDE a Canvas
    /// (RectTransform). They always:
    ///   * Set anchor and pivot to the same point (so the math is local to the parent).
    ///   * Set a positive sizeDelta (a zero-size RectTransform renders nothing visible,
    ///     and a Canvas background can collapse into a 2-pixel cross at the pivot).
    ///   * Position children via <c>RectTransform.anchoredPosition</c>, never via
    ///     <c>Transform.localPosition</c> (those two are not interchangeable in UI).
    /// </para>
    ///
    /// <para>The grid is row-major, top-to-bottom, left-to-right, anchored at the
    /// parent's top-left corner (0, 1). Pass <c>originX</c>/<c>originY</c> to offset the
    /// whole grid within a larger panel.</para>
    /// </summary>
    public static class UIGridBuilder
    {
        /// <summary>Build a container RectTransform at the given size and pivot.</summary>
        public static RectTransform CreateBoard(Transform parent, string name,
            int cols, int rows,
            float cellWidth, float cellHeight,
            float spacingX = 0f, float spacingY = 0f,
            Vector2 originPx = default)
        {
            // Width / height in pixels including all gaps. Origin defaults to top-left
            // of the parent (anchorMin/Max = (0,1), pivot = (0,1)) so the formula
            //   col * (cell + spacing),  -row * (cell + spacing)
            // places cell (0,0) at the top-left of the grid.
            float boardW = cols * cellWidth  + Mathf.Max(0, cols - 1) * spacingX;
            float boardH = rows * cellHeight + Mathf.Max(0, rows - 1) * spacingY;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(boardW, boardH);   // CRITICAL: never (0,0)
            rt.anchoredPosition = originPx;              // offset inside parent
            return rt;
        }

        /// <summary>
        /// Place <paramref name="child"/> at grid (col, row). The child must already
        /// have a RectTransform and must already be parented to <paramref name="board"/>.
        /// </summary>
        public static void PlaceCell(RectTransform child, int col, int row,
            float cellWidth, float cellHeight,
            float spacingX = 0f, float spacingY = 0f)
        {
            // Anchor + pivot = top-left so the cell's (0,0) sits at the cell origin
            // and the cell grows right/down. Mismatching pivot is the #1 cause of
            // "grid looks half-aligned" bugs.
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot     = new Vector2(0f, 1f);
            child.sizeDelta = new Vector2(cellWidth, cellHeight);
            child.anchoredPosition = new Vector2(
                col * (cellWidth  + spacingX),
               -row * (cellHeight + spacingY));
        }

        /// <summary>
        /// Convenience: instantiate a prefab with a RectTransform and place it at (col,row).
        /// </summary>
        public static RectTransform SpawnCell(GameObject prefab, RectTransform board,
            int col, int row,
            float cellWidth, float cellHeight,
            float spacingX = 0f, float spacingY = 0f)
        {
            if (prefab == null) return null;

            // Instantiate THEN SetParent(board, worldPositionStays:false) is the
            // canonical UI pattern. The 2-arg overload Instantiate(prefab, parent)
            // also works but copies world position which can leak prefab origin.
            var go = Object.Instantiate(prefab);
            go.transform.SetParent(board, worldPositionStays: false);
            go.name = prefab.name; // strip "(Clone)"

            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();

            PlaceCell(rt, col, row, cellWidth, cellHeight, spacingX, spacingY);
            return rt;
        }
    }
}