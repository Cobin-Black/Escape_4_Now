using System;
using Escape4Now.Map;

namespace Escape4Now.Multiplayer
{
    //Only these fields are accepted from the network. No object types or code are deserialized.
    [Serializable]
    public sealed class LanMessage
    {
        //Older builds do not support shared tiles, keys, and battle results.
        public const int Version = 16;
        public int version = Version;
        public string kind;
        public string code;
        public string action;
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
        public float secondsRemaining;
        public LanPlayerState[] players;
        public LanObjectState[] objects;
    }

    [Serializable]
    public sealed class LanPlayerState
    {
        public int x, y, roll, moves, item;
        public float worldX, worldY;
        public bool rolled, moving, won, protectedFromPlayers, hasKey;
    }

    [Serializable]
    public sealed class LanObjectState
    {
        public int id, kind, type, x, y, facing;
        public bool interactable, containsKey;
    }
}
