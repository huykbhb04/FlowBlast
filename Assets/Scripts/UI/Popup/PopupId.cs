namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Identifiers for popups managed by <see cref="FlowBlast.Managers.PopupManager"/>.
    /// Adding a new popup = new enum value + new class deriving BasePopup + new prefab + new registry entry.
    /// </summary>
    public enum PopupId
    {
        Pause = 0,
        Win = 1,
        Lose = 2,
    }
}