//Plays the assigned outdoor cues without changing movement, inventory or event placement.
using System;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalWorldFeedback : MonoBehaviour
    {
        [Serializable] private sealed class Cue { public SurvivalEventType type; public SurvivalWorldBurst prefab; }
        [Header("Outdoor Feedback Prefabs")]
        [SerializeField] private Cue[] cues = new Cue[0];
        public void Emit(SurvivalEventType type, PlayerCharacter player)
        {
            if (player == null || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            foreach (var cue in cues)
                if (cue.type == type && cue.prefab != null)
                {
                    var effect = Instantiate(cue.prefab,player.transform.position,Quaternion.identity,transform);
                    effect.Follow(player.transform);
                    return;
                }
        }
    }
}
