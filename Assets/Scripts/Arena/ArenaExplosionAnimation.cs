using UnityEngine;

namespace Escape4Now.Arena
{
    // A soft flash and round ground shockwave accompany the editable sparks and smoke.
    public sealed class ArenaExplosionAnimation : MonoBehaviour
    {
        [Header("Saved Visual References")]
        [SerializeField]
        private MeshRenderer flash;
        [SerializeField]
        private LineRenderer shockwave;
        [Header("Brief Flash")]
        [SerializeField, Min(.01f)]
        private float flashSeconds = .18f;
        [SerializeField]
        private Color flashColor = new Color(1, .85f, .35f, .95f);
        [SerializeField]
        private AnimationCurve flashSize = AnimationCurve.EaseInOut(0, .7f, 1, 1.3f);
        [Header("Ground Shockwave")]
        [SerializeField, Min(.01f)]
        private float waveSeconds = .48f;
        [SerializeField]
        private Color waveColor = new Color(1, .6f, .1f, .85f);
        [SerializeField]
        private AnimationCurve waveSize = AnimationCurve.EaseInOut(0, .3f, 1, 2.6f);
        [SerializeField, Range(16, 96)]
        private int waveSegments = 48;
        [SerializeField, Range(.1f, 1)]
        private float groundPerspective = .5f;
        [SerializeField, Min(.005f)]
        private float waveWidth = .035f;
        private System.Func<bool> paused;
        private float age;
        private MaterialPropertyBlock tint;
        public void ConfigurePause(System.Func<bool> context)
        {
            paused = context;
        }

        private void Awake()
        {
            tint = new MaterialPropertyBlock();
            shockwave.useWorldSpace = false;
            shockwave.loop = true;
            shockwave.positionCount = waveSegments;
            shockwave.startWidth = shockwave.endWidth = waveWidth;
            for (int i = 0; i < waveSegments; i++)
            {
                float angle = i * Mathf.PI * 2 / waveSegments;
                shockwave.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * groundPerspective, 0));
            }
        }

        private void Update()
        {
            if (paused != null && paused())
                return;
            age += Time.deltaTime;
            float f = Mathf.Clamp01(age / flashSeconds), w = Mathf.Clamp01(age / waveSeconds);
            flash.enabled = f < 1;
            shockwave.enabled = w < 1;
            var color = flashColor;
            color.a *= 1 - f;
            tint.SetColor("_Color", color);
            flash.SetPropertyBlock(tint);
            flash.transform.localScale = Vector3.one * flashSize.Evaluate(f);
            color = waveColor;
            color.a *= 1 - w;
            shockwave.startColor = shockwave.endColor = color;
            shockwave.transform.localScale = Vector3.one * (waveSize.Evaluate(w) * .5f);
        }
    }
}
