//A short visual/audio cue. Its lifetime is presentation only; event turns remain in the event controller.
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalWorldBurst : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private AudioSource sound;
        [SerializeField, Min(.1f)] private float lifetime = 1f;
        [SerializeField] private bool followPlayer;
        [Header("Optional Pickup Motion")]
        [SerializeField] private Transform pickupVisual;
        [SerializeField] private float pickupRise = .5f;
        [SerializeField] private float pickupSpin = 180f;
        private Transform target;
        private float elapsed;
        private bool paused;
        public void Follow(Transform player) { target = player; }
        private void Update()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) { Destroy(gameObject); return; }
            bool pause = Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused;
            if (pause != paused)
            {
                paused = pause;
                if (particles != null) { if (pause) particles.Pause(); else particles.Play(); }
                if (sound != null) { if (pause) sound.Pause(); else sound.UnPause(); }
            }
            if (paused) return;
            if (followPlayer && target != null) transform.position = target.position;
            elapsed += Time.deltaTime;
            if (pickupVisual != null)
            {
                float progress = Mathf.Clamp01(elapsed/lifetime);
                pickupVisual.localPosition = Vector3.up*pickupRise*progress;
                pickupVisual.localScale = Vector3.one*(1-progress);
                pickupVisual.localRotation = Quaternion.Euler(0,0,pickupSpin*progress);
            }
            if (elapsed >= lifetime) Destroy(gameObject);
        }
    }
}
