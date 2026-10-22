using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class EventFeedbackUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text message;
        [SerializeField] private Image blackout;
        [SerializeField] private Material blackoutMaterial;
        [SerializeField] private Toggle tutorialEventLocations;
        [Header("Blackout")]
        [SerializeField, Min(0.1f)] private float visibleRadiusTiles = 2f;
        [SerializeField, Range(0f, 0.99f)] private float clearRadiusRatio = 0.7f;
        [SerializeField] private string remainingFormat = "\nLights return in {0} player turns.";
        private Material instance;
        private static readonly int Center = Shader.PropertyToID("_Center");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int ClearRatio = Shader.PropertyToID("_ClearRatio");

        private void Awake()
        {
            if (blackoutMaterial == null) return;
            instance = new Material(blackoutMaterial);
            blackout.material = instance;
        }

        private void LateUpdate()
        {
            var events = context.Events;
            bool playing = context.Playing && !context.Finished && events != null;
            tutorialEventLocations.gameObject.SetActive(playing && context.Session == null);
            if (tutorialEventLocations.gameObject.activeSelf)
                tutorialEventLocations.SetIsOnWithoutNotify(events.EventLocationsVisible);
            panel.SetActive(playing && !string.IsNullOrEmpty(events.CurrentMessage));
            if (panel.activeSelf)
                message.text = events.CurrentMessage + (events.BlackoutTurns > 0 ? string.Format(remainingFormat, events.BlackoutTurns) : "");
            bool dark = playing && events.BlackoutTurns > 0;
            blackout.gameObject.SetActive(dark);
            if (!dark || instance == null) return;
            var player = context.ActivePlayer;
            var camera = context.WorldCamera;
            float radius = 0f;
            Vector3 center = Vector3.zero;
            if (player != null && camera != null)
            {
                center = camera.WorldToScreenPoint(player.transform.position);
                float tileWidth = player.GridMap != null
                    ? Mathf.Abs((player.GridMap.GridToWorld(Vector2Int.right) - player.GridMap.GridToWorld(Vector2Int.left)).x) : 1f;
                var edge = camera.WorldToScreenPoint(player.transform.position + Vector3.right * tileWidth * visibleRadiusTiles);
                radius = Mathf.Abs(edge.x - center.x);
            }
            instance.SetVector(Center, center);
            instance.SetFloat(Radius, radius);
            instance.SetFloat(ClearRatio, clearRadiusRatio);
        }

        public void SetTutorialEventLocations(bool visible)
        {
            if (context.Session == null && context.Events != null)
                context.Events.SetEventLocationsVisible(visible);
        }

        private void OnDestroy()
        {
            if (instance != null) Destroy(instance);
        }
    }
}
