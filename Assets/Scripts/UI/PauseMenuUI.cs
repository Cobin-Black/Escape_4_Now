using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class PauseMenuUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject pauseButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject displayOptions;
        [SerializeField] private Toggle events;
        [SerializeField] private Toggle items;
        [SerializeField] private Toggle key;

        private void Start()
        {
            if (context.Session == null) return;
            events.onValueChanged.AddListener(context.Session.SetEventTilesVisible);
            items.onValueChanged.AddListener(context.Session.SetItemObstaclesVisible);
            key.onValueChanged.AddListener(context.Session.SetKeyLocationVisible);
        }

        private void LateUpdate()
        {
            bool running = context.Session != null && context.Playing && !context.Finished;
            pauseButton.SetActive(running && !LocalMultiplayerSession.GameplayPaused);
            panel.SetActive(running && LocalMultiplayerSession.GameplayPaused);
            if (!panel.activeSelf) return;
            displayOptions.SetActive(context.Session.ShowDisplayOptions);
            events.SetIsOnWithoutNotify(context.Session.ShowEventTiles);
            items.SetIsOnWithoutNotify(LocalMultiplayerSession.ShowItemObstacles);
            key.SetIsOnWithoutNotify(LocalMultiplayerSession.ShowKeyLocation);
        }

        public void Pause() { context.Session?.PauseGame(); }
        public void Resume() { context.Session?.ResumeGame(); }
        public void Leave() { context.Session?.ReturnToSetup(); }
    }
}
