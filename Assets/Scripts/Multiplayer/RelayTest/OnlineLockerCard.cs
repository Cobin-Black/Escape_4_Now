using Escape4Now.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // A local-style locker frame with room-specific empty and occupied states.
    public sealed class OnlineLockerCard : MonoBehaviour
    {
        [SerializeField] private GameObject occupied, empty;
        [SerializeField] private Text emptyLabel, skin;
        [SerializeField] private Button previous, next;

        // Connect this card's skin arrows to the local room profile.
        public void Bind(OnlineRaceSession session)
        {
            previous.onClick.AddListener(() => session.CycleProfileSkin(-1));
            next.onClick.AddListener(() => session.CycleProfileSkin(1));
        }

        // Show the correct occupied, open, bot or closed card and allow only local skin changes.
        public void Present(bool connected, bool available, bool bot, bool local, string skinLabel, Color color)
        {
            GetComponent<PlayerColorView>()?.SetColor(color);
            occupied.SetActive(connected);
            empty.SetActive(!connected);
            emptyLabel.text = !available ? "CLOSED" : bot ? "BOT SLOT" : "AWAITING PLAYER";
            previous.gameObject.SetActive(local);
            next.gameObject.SetActive(local);
            skin.text = skinLabel;
        }
    }
}
