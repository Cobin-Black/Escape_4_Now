using UnityEngine;

namespace Escape4Now.Arena
{
    // Small ground rings replace floating names. The holder also gets a pulsing gold outer ring.
    [RequireComponent(typeof(ArenaParticipant))]
    public sealed class ArenaPlayerIndicator : MonoBehaviour
    {
        [Header("Ground Ring")]
        [SerializeField, Min(.1f)]
        private float radius = .38f;
        [SerializeField, Min(.005f)]
        private float width = .035f;
        [SerializeField, Range(16, 64)]
        private int segments = 32;
        [Header("Potato Holder")]
        [SerializeField]
        private Color holderColor = new Color(1, .72f, .12f);
        [SerializeField, Min(1)]
        private float holderRadiusMultiplier = 1.3f;
        [SerializeField, Min(0)]
        private float pulseStrength = .06f;
        [SerializeField, Min(.1f)]
        private float pulseSpeed = 3;
        private ArenaParticipant player;
        private LineRenderer ring, holder;
        private float clock;
        private void Awake()
        {
            player = GetComponent<ArenaParticipant>();
        }

        private LineRenderer Create(string label)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = player.Match.Events != null && player.Match.Events.Pool != null && player.Match.Events.Pool.visuals != null ? player.Match.Events.Pool.visuals.lineMaterial : player.Match.Map.Settings.material;
            return line;
        }

        private void LateUpdate()
        {
            if (player.Match == null || !player.Alive)
                return;
            if (ring == null)
            {
                ring = Create("Player color ring");
                holder = Create("Potato holder ring");
            }

            if (!player.Match.Paused)
                clock += Time.deltaTime;
            Draw(ring, radius, player.Character.IdentityColor);
            bool carrying = player.Match is HotPotatoController mode && mode.Holder == player && !mode.BetweenRounds;
            holder.enabled = carrying;
            if (carrying)
                Draw(holder, radius * holderRadiusMultiplier * (1 + Mathf.Sin(clock * pulseSpeed) * pulseStrength), holderColor);
        }

        private void Draw(LineRenderer line, float size, Color color)
        {
            line.startColor = line.endColor = color;
            line.sortingOrder = player.Match.Map.Order(player.Position) - 1;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                line.SetPosition(i, player.Match.Map.World(player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size));
            }
        }
    }
}
