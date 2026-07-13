using FlowBlast.Managers;
using UnityEngine;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Lightweight wrapper whose ONLY purpose is to expose popup actions as
    /// single-overload public methods that Unity's Button OnClick dropdown
    /// can reliably list, regardless of namespace / generic quirks.
    ///
    /// Place this on the SAME GameObject as the PopupManager. Drag this
    /// component (not the PopupManager) into the Button.OnClick object slot.
    /// </summary>
    [AddComponentMenu("FlowBlast/Popup Trigger")]
    public class PopupTrigger : MonoBehaviour
    {
        public void OpenPausePopup()
        {
            PopupManager.Instance?.OpenPausePopup();
        }

        public void OpenWinPopup()
        {
            PopupManager.Instance?.OpenWinPopup();
        }

        public void OpenLosePopup()
        {
            PopupManager.Instance?.OpenLosePopup();
        }

        public void ClosePausePopup()
        {
            PopupManager.Instance?.ClosePausePopup();
        }

        public void CloseWinPopup()
        {
            PopupManager.Instance?.CloseWinPopup();
        }

        public void CloseLosePopup()
        {
            PopupManager.Instance?.CloseLosePopup();
        }

        public void CloseTopPopup()
        {
            PopupManager.Instance?.CloseTopPopup();
        }

        public void CloseAll()
        {
            PopupManager.Instance?.CloseAll();
        }

        // Convenience: toggle pause (open if closed, close-top if open).
        public void TogglePausePopup()
        {
            PopupManager pm = PopupManager.Instance;
            if (pm == null) return;
            if (pm.IsAnyPopupOpen) pm.CloseTopPopup();
            else pm.OpenPausePopup();
        }
    }
}