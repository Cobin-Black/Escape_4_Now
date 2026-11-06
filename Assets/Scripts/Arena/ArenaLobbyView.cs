using System.Collections.Generic;
using Escape4Now.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.Arena
{
    // Only setup presentation and UI choices live here. ArenaMatch still creates and validates the players.
    public sealed class ArenaLobbyView : MonoBehaviour
    {
        [SerializeField]
        private ArenaInterfaceStyle style;
        [SerializeField]
        private Text title, instructions, error;
        [SerializeField]
        private Button[] playerCounts;
        [SerializeField]
        private GameObject[] rows;
        [SerializeField]
        private InputField[] names;
        [SerializeField]
        private Dropdown[] controls, skins;
        [SerializeField]
        private Image[] portraits, identityStrips;
        [SerializeField]
        private Button start;
        [Header("Setup Defaults and Labels")]
        [SerializeField, Range(2, 4)]
        private int playerCount = 2;
        [SerializeField]
        private string defaultSkinLabel = "Default";
        [SerializeField]
        private string keyboardFormat = "Keyboard: {0}";
        [SerializeField]
        private string gamepadFormat = "Gamepad {0}";
        [SerializeField]
        private string[] movementHints =
        {
            "WASD",
            "Arrow keys",
            "IJKL",
            "Numpad 8456"
        };
        [SerializeField]
        private string[] tagHints =
        {
            "WASD + F",
            "Arrows + Right Ctrl",
            "IJKL + O",
            "Numpad 8456 + 0"
        };
        private ArenaMatch match;
        public int PlayerCount => playerCount;

        // Connect saved Unity widgets to the existing local setup options.
        public void Initialize(ArenaMatch owner)
        {
            match = owner;
            title.text = match.Title;
            instructions.text = match.Instructions;
            for (int i = 0; i < playerCounts.Length; i++)
            {
                int count = i + 2;
                playerCounts[i].onClick.AddListener(() =>
                {
                    playerCount = count;
                    Refresh();
                });
            }

            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i;
                var skinOptions = new List<string>
                {
                    defaultSkinLabel
                };
                if (match.Skins != null)
                    for (int skin = 0; skin < match.Skins.Count; skin++)
                        skinOptions.Add(match.Skins.GetSkin(skin).skinName);
                skins[i].ClearOptions();
                skins[i].AddOptions(skinOptions);
                skins[i].onValueChanged.AddListener(value => RefreshPortrait(slot));
                var deviceOptions = new List<string>
                {
                    string.Format(keyboardFormat, match is HotPotatoController ? tagHints[i] : movementHints[i])
                };
                for (int pad = 1; pad <= 4; pad++)
                    deviceOptions.Add(string.Format(gamepadFormat, pad));
                controls[i].ClearOptions();
                controls[i].AddOptions(deviceOptions);
                identityStrips[i].color = match.GetPlayerColor(i);
                RefreshPortrait(i);
            }

            start.onClick.AddListener(StartMatch);
            AccountLinkUI.Create(gameObject, names, null);
            Refresh();
        }

        // A two-player lobby takes only two rows. Layout groups move the footer up automatically.
        private void Refresh()
        {
            for (int i = 0; i < rows.Length; i++)
                rows[i].SetActive(i < playerCount);
            for (int i = 0; i < playerCounts.Length; i++)
            {
                bool selected = i + 2 == playerCount;
                playerCounts[i].image.color = selected ? style.accent : style.input;
                playerCounts[i].GetComponentInChildren<Text>().color = selected ? style.panel : style.text;
            }
        }

        // Preview the selected existing skin without changing the match skin assignments.
        private void RefreshPortrait(int slot)
        {
            int chosen = skins[slot].value - 1;
            var skin = match.Skins != null ? match.Skins.GetSkin(chosen >= 0 ? chosen : slot) : null;
            portraits[slot].sprite = skin != null ? skin.front : null;
            portraits[slot].enabled = portraits[slot].sprite != null;
        }

        public void StartMatch()
        {
            var labels = new string[playerCount];
            var devices = new int[playerCount];
            var looks = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                labels[i] = names[i].text;
                devices[i] = controls[i].value - 1;
                looks[i] = skins[i].value - 1;
            }

            match.StartMatch(playerCount, labels, devices, looks);
            error.text = match.Error;
            error.gameObject.SetActive(!string.IsNullOrEmpty(match.Error));
        }
    }
}
