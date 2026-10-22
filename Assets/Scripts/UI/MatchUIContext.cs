using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.UI
{
    public sealed class MatchUIContext : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private LocalMultiplayerSession session;
        [SerializeField] private PlayerCharacter standalonePlayer;
        [SerializeField, Tooltip("Used by standalone scenes; matches use their mode's turn system.")]
        private TurnSystemController turns;
        [SerializeField, Tooltip("Used by standalone scenes; matches use their mode's timer.")]
        private GameTimer timer;
        [SerializeField] private MapEventController mapEvents;
        [SerializeField] private Camera worldCamera;
        public LocalMultiplayerSession Session => session;
        public TurnSystemController Turns => session != null && session.Mode != null ? session.Mode.Turns : turns;
        public GameTimer Timer => session != null && session.Mode != null ? session.Mode.Timer : timer;
        public MapEventController Events => mapEvents;
        public Camera WorldCamera => worldCamera;
        public bool Playing => session == null || session.MatchStarted;
        public bool Finished => LocalMultiplayerSession.MatchHasEnded;
        public PlayerCharacter ActivePlayer
        {
            get
            {
                if (!Playing) return null;
                if (session == null) return standalonePlayer;
                var players = session.Participants;
                if (players == null) return null;
                foreach (var player in players)
                    if (player != null && player.isActiveAndEnabled && player.IsCurrentTurn) return player;
                return null;
            }
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        public static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, Screen.height - corners[2].y,
                corners[2].x, Screen.height - corners[0].y);
        }
    }
}
