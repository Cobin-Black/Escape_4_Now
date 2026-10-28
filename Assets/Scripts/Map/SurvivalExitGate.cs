// Shows the closed or open campus gate. Puzzle completion is checked by the Survival mode.

using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalExitGate : MonoBehaviour
    {
        [Header("Authored Gate Geometry")]
        [SerializeField] private SurvivalPropVisual closedGate;
        [SerializeField] private SurvivalPropVisual openGate;
        [Header("Gate Labels")]
        [SerializeField] private TextMesh statusLabel;
        [SerializeField] private string lockedLabel = "LOCKED";
        [SerializeField] private string unlockedLabel = "EXIT";
        [Header("Gate Signal Light")]
        [SerializeField] private SpriteRenderer signalLight;
        [SerializeField] private Color lockedLightColor = new Color(1f, .28f, .12f);
        [SerializeField] private Color unlockedLightColor = new Color(.28f, 1f, .4f);
        private SurvivalMap map;
        private Vector2Int boundary;
        private int quarterTurns;
        public bool IsUnlocked { get; private set; }

        // Face the physical gate along the boundary chosen by the objective layout.
        public void Configure(SurvivalMap source, Vector2Int approach)
        {
            map = source;
            boundary = SurvivalObjectiveLayout.ExitBoundary(approach, map.Width, map.Height);
            quarterTurns = boundary.x == 0 || boundary.x == map.Width - 1 ? 1 : 0;
            SetUnlocked(false);
        }

        // Change only the visible gate state; the Survival mode still checks whether puzzles unlocked the exits.
        public void SetUnlocked(bool unlocked)
        {
            if (map == null) return;
            IsUnlocked = unlocked;
            if (statusLabel != null) statusLabel.text = unlocked ? unlockedLabel : lockedLabel;
            if (signalLight != null)
            {
                signalLight.color = unlocked ? unlockedLightColor : lockedLightColor;
                signalLight.sortingOrder = 502 - (boundary.x + boundary.y) * 10;
            }
            closedGate.gameObject.SetActive(!unlocked);
            openGate.gameObject.SetActive(unlocked);
            var visual = unlocked ? openGate : closedGate;
            visual.Build(map.TileSize, Vector2.one, map.MapMaterial,
                500 - (boundary.x + boundary.y) * 10, quarterTurns: quarterTurns);
        }
    }
}
