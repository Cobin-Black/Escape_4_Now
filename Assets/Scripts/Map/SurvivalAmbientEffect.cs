// Controls outdoor particles and sound. Weather fits the campus, and Pause stops its presentation.

using Escape4Now.Multiplayer;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalAmbientEffect : MonoBehaviour
    {
        [Header("Authored Effect References")]
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private AudioSource sound;
        [Header("Campus Weather Coverage")]
        [SerializeField] private bool fitCampus;
        [SerializeField, Min(0f)] private float particlesPerTile = .3f;
        [SerializeField, Min(1)] private int maximumEmission = 350;
        [SerializeField, Min(0f)] private float weatherHeight = .6f;
        [SerializeField, Min(0f), Tooltip("Zero means the event controller owns expiry.")] private float lifetimeSeconds;
        private float remaining;
        private bool paused;
        private Mesh weatherShape;
        public bool IsCampusWeather => fitCampus;
        private void OnEnable() { remaining = lifetimeSeconds; }

        // Fit weather to the generated campus diamond so it does not fill the enclosing rectangle outside the fences.
        public void Configure(SurvivalMap map)
        {
            if (!fitCampus || particles == null) return;
            transform.position = map.GridToWorld(new Vector2((map.Width - 1) * .5f, (map.Height - 1) * .5f));
            var shape = particles.shape;
            //Emit over the campus diamond rather than an enclosing rectangle outside its fences.
            weatherShape = new Mesh { name = "Generated campus weather coverage" };
            var corners = new[] { Vector2Int.zero, new Vector2Int(map.Width - 1, 0),
                new Vector2Int(map.Width - 1, map.Height - 1), new Vector2Int(0, map.Height - 1) };
            var vertices = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
                vertices[i] = particles.transform.InverseTransformPoint(map.GridToWorld(corners[i]) + Vector3.up * weatherHeight);
            weatherShape.vertices = vertices; weatherShape.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            weatherShape.RecalculateNormals(); weatherShape.RecalculateBounds();
            shape.shapeType = ParticleSystemShapeType.Mesh; shape.mesh = weatherShape;
            shape.scale = Vector3.one;
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Min(maximumEmission, map.Width * map.Height * particlesPerTile);
        }

        // Pause sound and particles with gameplay; this visual lifetime does not control the event's turn duration.
        private void Update()
        {
            bool shouldPause = LocalMultiplayerSession.GameplayPaused || LocalMultiplayerSession.MatchHasEnded;
            if (!shouldPause && lifetimeSeconds > 0f && (remaining -= Time.deltaTime) <= 0f) { Destroy(gameObject); return; }
            if (paused == shouldPause) return;
            paused = shouldPause;
            if (particles != null) { if (paused) particles.Pause(); else particles.Play(); }
            if (sound != null) { if (paused) sound.Pause(); else sound.UnPause(); }
        }
        private void OnDestroy() { if (weatherShape != null) Destroy(weatherShape); }
    }
}
