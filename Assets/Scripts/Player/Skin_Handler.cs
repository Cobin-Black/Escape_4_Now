using System;
using UnityEngine;

namespace Escape4Now.Player
{
    //One character skin. The front and back sprites always stay together as a pair.
    [Serializable]
    public sealed class PlayerSkin
    {
        public string skinName = "Skin";
        public Sprite front;
        public Sprite back;
        public bool IsValid => front != null && back != null;
    }

    //Keeps track of which skin each player is using. Add it next to a gamemode's match startup script
    //and call ApplyTo for every participant when the match starts.
    [DisallowMultipleComponent, AddComponentMenu("Escape 4 Now/Player/Skin Handler")]
    public sealed class Skin_Handler : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public const int NoSkin = -1;

        [Header("Skins (front and back pairs, in sprite sheet order)")]
        [SerializeField] private PlayerSkin[] skins = new PlayerSkin[0];

        //Skin index chosen by each player slot. NoSkin means the player has not picked one yet.
        private readonly int[] chosen = new int[MaxPlayers];

        public int Count => skins != null ? skins.Length : 0;

        private void Awake() { ResetToDefaults(); }

        //Player 1 starts with the first skin, Player 2 with the second, and so on.
        public void ResetToDefaults()
        {
            for (int i = 0; i < chosen.Length; i++)
                chosen[i] = i < Count && skins[i] != null && skins[i].IsValid ? i : NoSkin;
        }

        //Clears every player's skin so all of them can be picked again.
        public void ClearAll()
        {
            for (int i = 0; i < chosen.Length; i++) chosen[i] = NoSkin;
        }

        public PlayerSkin GetSkin(int skinIndex)
        {
            return skinIndex >= 0 && skinIndex < Count ? skins[skinIndex] : null;
        }

        public int GetSkinIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < chosen.Length ? chosen[playerIndex] : NoSkin;
        }

        public PlayerSkin GetPlayerSkin(int playerIndex) => GetSkin(GetSkinIndex(playerIndex));

        //Returns the player slot using this skin, or -1 when nobody has it.
        public int GetOwner(int skinIndex)
        {
            if (skinIndex == NoSkin) return -1;
            for (int i = 0; i < chosen.Length; i++)
                if (chosen[i] == skinIndex) return i;
            return -1;
        }

        public bool IsTaken(int skinIndex) => GetOwner(skinIndex) >= 0;

        //Gives a player a skin. Fails when another player already has it, so skins are never duplicated.
        public bool TryAssign(int playerIndex, int skinIndex)
        {
            if (playerIndex < 0 || playerIndex >= chosen.Length) return false;
            PlayerSkin skin = GetSkin(skinIndex);
            if (skin == null || !skin.IsValid) return false;
            int owner = GetOwner(skinIndex);
            if (owner >= 0 && owner != playerIndex) return false;
            chosen[playerIndex] = skinIndex;
            return true;
        }

        //Gives any player slot without a skin the first skin nobody is using.
        //Keeps the match startable if the player count is raised after picking.
        public void FillMissing()
        {
            for (int player = 0; player < chosen.Length; player++)
            {
                if (GetPlayerSkin(player) != null) continue;
                chosen[player] = NoSkin;
                for (int skin = 0; skin < Count; skin++)
                    if (!IsTaken(skin) && TryAssign(player, skin)) break;
            }
        }

        //True when the first playerCount players each have a different skin.
        public bool AllAssigned(int playerCount)
        {
            for (int i = 0; i < playerCount && i < chosen.Length; i++)
                if (GetPlayerSkin(i) == null) return false;
            return true;
        }

        //Puts the player's chosen skin on their character.
        public void ApplyTo(PlayerCharacter player, int playerIndex)
        {
            PlayerSkin skin = GetPlayerSkin(playerIndex);
            if (player == null || player.Appearance == null || skin == null || !skin.IsValid) return;
            player.Appearance.ApplySkin(skin.front, skin.back);
        }

        private void OnValidate()
        {
            if (skins == null) skins = new PlayerSkin[0];
        }
    }
}
