// Connects the saved Pause menu buttons and reveal toggles to the existing session and mode systems.

using Escape4Now.Multiplayer;
using Escape4Now.GameModes;
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
        [SerializeField, Tooltip("Survival task labels; leave unassigned in other modes.")] private Toggle tasks;

        // Connect the saved reveal toggles to the current mode, including Survival task labels.
        private void Start()
        {
            if (context.Session == null) return;
            events.onValueChanged.AddListener(context.Session.SetEventTilesVisible);
            items.onValueChanged.AddListener(context.Session.SetItemObstaclesVisible);
            key.onValueChanged.AddListener(context.Session.SetKeyLocationVisible);
            if (tasks != null)
                tasks.onValueChanged.AddListener(value =>
                {
                    if (context.Session.Mode is SurvivalModeController survival)
                        survival.Tasks.SetTaskNamesVisible(value);
                });
        }

        private void LateUpdate()
        {
            bool running = context.Session != null && context.Playing && !context.Finished;
            pauseButton.SetActive(running && !LocalMultiplayerSession.GameplayPaused);
            panel.SetActive(running && LocalMultiplayerSession.GameplayPaused);
            if (!panel.activeSelf) return;
            var survival = context.Session.Mode as SurvivalModeController;
            displayOptions.SetActive(context.Session.ShowDisplayOptions);
            events.SetIsOnWithoutNotify(context.Events != null && context.Events.EventLocationsVisible);
            items.gameObject.SetActive(survival == null);
            key.gameObject.SetActive(survival == null);
            if (tasks != null)
            {
                tasks.gameObject.SetActive(survival != null);
                if (survival != null) tasks.SetIsOnWithoutNotify(survival.Tasks.TaskNamesVisible);
            }
            items.SetIsOnWithoutNotify(LocalMultiplayerSession.ShowItemObstacles);
            key.SetIsOnWithoutNotify(LocalMultiplayerSession.ShowKeyLocation);
        }

        public void Pause() { context.Session?.PauseGame(); }
        public void Resume() { context.Session?.ResumeGame(); }
        public void Leave() { context.Session?.ReturnToSetup(); }
    }
}
