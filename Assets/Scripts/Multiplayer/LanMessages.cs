// Data sent between the LAN host and clients for setup, player actions and shared match state.

using System;
using Escape4Now.Map;

namespace Escape4Now.Multiplayer
{
    //Only these fields are accepted from the network. No object types or code are deserialized.
    [Serializable]
    public sealed class LanMessage
    {
        //Older builds do not share who is in a key battle or the rolls for the dice shown above them.
        public const int Version = 18;
        public int version = Version;
        public string kind;
        public string code;
        public string action;
        public string modeState;
        public int player, count, sequence, turn, round, x, y, value;
        public LanSnapshot state;
    }

    [Serializable]
    public sealed class LanSnapshot
    {
        public int seed, exitX, exitY, turn, round, blackout;
        public bool paused;
        public SchoolMapGenerationSettings mapSettings;
        public SchoolFurnitureSettings furnitureSettings;
        public string message;
        public string battleMessage = "";
        //The current key battle, so every screen can roll the same two dice above the right players.
        public int battleNumber, battleChallengerRoll, battleOpponentRoll;
        public int battleChallenger = -1, battleOpponent = -1;
        public bool storageOpen;
        public string storageName = "";
        public int storageItem;
        public float secondsRemaining;
        public LanPlayerState[] players;
        public int[] turnOrder;
        public LanObjectState[] objects;
    }

    [Serializable]
    public sealed class LanPlayerState
    {
        public int x, y, roll, moves, item, firstDie, secondDie;
        public int health = -1;
        public string displayName;
        public int skin;
        public float worldX, worldY;
        public bool rolled, rolling, moving, won, protectedFromPlayers, hasKey;
    }

    [Serializable]
    public sealed class LanObjectState
    {
        public int id, kind, type, x, y, facing;
        public bool interactable, containsKey;
    }
}
