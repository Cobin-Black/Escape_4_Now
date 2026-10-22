using UnityEngine;

namespace Escape4Now.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class MatchCanvasVisibility : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas matchCanvas;
        [Header("Editor Preview")]
        [SerializeField, Tooltip("Show the authored interface outside Play Mode while editing its layout. Off by default.")]
        private bool previewInEditMode;

        private void OnEnable() => Refresh();
        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }
        private void Update() => Refresh();

        private void Refresh()
        {
            if (matchCanvas == null) matchCanvas = GetComponent<Canvas>();
            bool visible = Application.IsPlaying(gameObject) || previewInEditMode;
            if (matchCanvas != null && matchCanvas.enabled != visible) matchCanvas.enabled = visible;
        }

        private void OnDisable()
        {
            if (matchCanvas != null) matchCanvas.enabled = false;
        }
    }
}
