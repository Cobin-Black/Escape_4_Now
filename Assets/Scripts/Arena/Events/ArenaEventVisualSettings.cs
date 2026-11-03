using UnityEngine;

namespace Escape4Now.Arena
{
    [CreateAssetMenu(menuName = "Escape 4 Now/Arena/Event Visual Settings")]
    public sealed class ArenaEventVisualSettings : ScriptableObject
    {
        [Header("Reuse Existing Materials and Environmental Prefabs")]
        public Material lineMaterial, blackoutMaterial;
        public GameObject wetFloorPrefab, hazardFloorPrefab, basketballPrefab, speakerPrefab;
        [Header("Show Events / Small Location Indicators")]
        [Min(.1f)]
        public float assistanceRadius = .38f;
        [Header("Warning and Ground Markings")]
        public Color warningColor = new Color(1, .65f, .1f, .9f);
        [Min(.005f)]
        public float lineWidth = .05f;
        [Range(16, 96)]
        public int circleSegments = 48;
        [Min(.1f)]
        public float warningPulseSpeed = 2;
        public int floorSortingOrder = -650;
        [Header("Blackout — Same Visibility For Everyone")]
        [Range(0, .65f)]
        public float blackoutOpacity = .48f;
        [Min(.1f)]
        public float fadeSpeed = 1.4f;
        [Header("Basketball Animation")]
        [Min(.01f)]
        public float ballRadius = .25f;
        [Min(0)]
        public float bounceHeight = .25f;
        [Min(.1f)]
        public float bounceRate = 4;
        [Header("Moving Obstacles and Lighting")]
        [Min(.05f)]
        public float obstacleAnimationSeconds = .5f;
        [Min(.1f)]
        public float movingLightSeconds = 3;
        [Header("Visible Spotlight Beams")]
        [Min(.1f)]
        public float spotlightRadius = .9f;
        [Min(.1f)]
        public float coneHeight = 2.5f;
        [Range(0, 1)]
        public float beamOpacity = .2f;
        [Range(0, 1)]
        public float lightPoolOpacity = .25f;
    }
}
