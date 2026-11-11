using Escape4Now.Accounts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Escape4Now.Arena
{
    // Coordinates screen visibility only. Setup and HUD presentation have their own components.
    public sealed class ArenaUI : MonoBehaviour
    {
        [Header("Mode and Views")]
        [SerializeField]
        private ArenaMatch match;
        [SerializeField]
        private ArenaLobbyView setup;
        [SerializeField]
        private ArenaHudView presentation;
        [SerializeField]
        private GameObject lobby, hud, pausePanel, resultPanel;
        [SerializeField]
        private Text result;
        [Header("Shared Menu Buttons")]
        [SerializeField]
        private Button pause, resume, leave, replay, menu;
        [Header("Event Map Assistance")]
        [SerializeField, Tooltip("Optional debug button. Events continue normally while this is disabled.")]
        private bool showEventButton;
        [SerializeField]
        private Button showEvents;
        [SerializeField]
        private Text showEventsLabel;
        [SerializeField]
        private string eventsOffLabel = "Show Events", eventsOnLabel = "Hide Events";
        [SerializeField]
        private string mainMenuScene = "MainMenu";
        [Header("Result Presentation")]
        [SerializeField, Min(0)]
        private float resultRevealDelay;
        private float resultAge;
        private void Start()
        {
            setup.Initialize(match);
            showEvents.onClick.AddListener(() => match.Events.SetEventLocationsVisible(!match.Events.EventLocationsVisible));
            pause.onClick.AddListener(() => match.SetPaused(true));
            resume.onClick.AddListener(() => match.SetPaused(false));
            leave.onClick.AddListener(match.ReturnToSetup);
            replay.onClick.AddListener(match.ReturnToSetup);
            menu.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));
        }

        // Screen changes follow the existing match state; none of these widgets decides who wins.
        private void LateUpdate()
        {
            bool finished = !string.IsNullOrEmpty(match.Result);
            lobby.SetActive(!match.Playing && !finished);
            hud.SetActive(match.Playing);
            pausePanel.SetActive(match.Playing && match.Paused);
            resultAge = finished ? resultAge + Time.unscaledDeltaTime : 0;
            resultPanel.SetActive(finished && resultAge >= resultRevealDelay);
            pause.gameObject.SetActive(match.Playing && !match.Paused);
            showEvents.gameObject.SetActive(showEventButton && match.Playing && !match.Paused);
            showEventsLabel.text = match.Events.EventLocationsVisible ? eventsOnLabel : eventsOffLabel;
            if (finished)
            {
                result.text = string.IsNullOrEmpty(AccountStats.Status) ? match.Result : match.Result + "\n" + AccountStats.Status;
                //Leaving waits for the save, because it swaps between the players' accounts.
                replay.interactable = menu.interactable = !AccountStats.IsSaving;
            }
            if (match.Playing)
                presentation.Present(match);
        }

        public void StartMatch() => setup.StartMatch();
    }
}
