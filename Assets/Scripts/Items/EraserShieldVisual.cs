
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class EraserShieldVisual : MonoBehaviour
    {
        [SerializeField] private Sprite eraserSprite;
        [SerializeField] private float orbitRadius = 0.7f;
        [SerializeField] private float orbitSpeed = 120f;
        [SerializeField] private float spriteScale = 1f;

        private ArenaParticipant player;
        private SpriteRenderer[] erasers = new SpriteRenderer[2];
        private float angle;

        private void Awake()
        {
            player = GetComponent<ArenaParticipant>();

            // Create two Eraser sprites.
            for (int i = 0; i < erasers.Length; i++)
            {
                GameObject eraser = new GameObject("Orbiting Eraser " + (i + 1));
                eraser.transform.SetParent(transform);
                eraser.transform.localScale = Vector3.one * spriteScale;

                erasers[i] = eraser.AddComponent<SpriteRenderer>();
                erasers[i].sprite = eraserSprite;
                erasers[i].sortingOrder = 100;
                erasers[i].enabled = false;
            }
        }

        private void Update()
        {
            if (player == null || player.Match == null)
                return;

            if (!player.Match.Playing)
            {
                HideErasers();
                return;
            }

            if (player.Match.Paused)
                return;

            angle += orbitSpeed * Time.deltaTime;

            // Display one Eraser for each remaining shield block.
            for (int i = 0; i < erasers.Length; i++)
            {
                bool visible = i < player.EraserBlocksRemaining;
                erasers[i].enabled = visible;

                if (!visible)
                    continue;

                float currentAngle = (angle + i * 180f) * Mathf.Deg2Rad;

                Vector3 position = new Vector3(
                    Mathf.Cos(currentAngle) * orbitRadius,
                    Mathf.Sin(currentAngle) * orbitRadius,
                    -0.1f
                );

                erasers[i].transform.localPosition = position;
            }
        }

        // Hide both Erasers when the shield is inactive.
        private void HideErasers()
        {
            foreach (SpriteRenderer eraser in erasers)
                eraser.enabled = false;
        }
    }
}
