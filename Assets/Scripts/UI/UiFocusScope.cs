// Gives an opened UI panel a sensible selection and clears or restores that selection when it closes.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class UiFocusScope : MonoBehaviour
    {
        [SerializeField] private Selectable firstSelection;
        [SerializeField, Tooltip("Leave off for gameplay modals so Enter returns to Interact/Key Battle instead of a previously clicked button.")]
        private bool restorePreviousSelection;
        private GameObject previous;

        private void OnEnable()
        {
            if (!Application.isPlaying || EventSystem.current == null) return;
            previous = EventSystem.current.currentSelectedGameObject;
            if (firstSelection != null) firstSelection.Select();
        }

        // Clear the closing panel's selection so a later Interact press cannot click its hidden button.
        private void OnDisable()
        {
            if (!Application.isPlaying || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(transform)) return;
            EventSystem.current.SetSelectedGameObject(restorePreviousSelection && previous != null && previous.activeInHierarchy ? previous : null);
        }
    }
}
