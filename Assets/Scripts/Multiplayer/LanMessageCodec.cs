// Turns LAN messages into JSON, leaving large map data out of small control messages.

using System;
using UnityEngine;

namespace Escape4Now.Multiplayer
{
    public static class LanMessageCodec
    {
        //Unity serializes null inline classes with their fields. Control packets must not
        //include the snapshot's map configuration or grow when new Inspector settings appear.
        [Serializable]
        private sealed class ControlMessage
        {
            public int version;
            public string kind, code, action;
            public int player, count, sequence, turn, round, x, y, value;

            public ControlMessage(LanMessage message)
            {
                version = message.version; kind = message.kind; code = message.code; action = message.action;
                player = message.player; count = message.count; sequence = message.sequence;
                turn = message.turn; round = message.round; x = message.x; y = message.y; value = message.value;
            }
        }

        // Only state messages need full map and player data; ordinary commands stay small.
        public static string Serialize(LanMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            return message.kind == "state" ? JsonUtility.ToJson(message)
                : JsonUtility.ToJson(new ControlMessage(message));
        }
    }
}
