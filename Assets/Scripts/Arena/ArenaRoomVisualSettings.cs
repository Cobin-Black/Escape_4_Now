using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Shared room art settings; each mode still chooses its own map and obstacle palette.
    [CreateAssetMenu(menuName = "Escape 4 Now/Arena/Room Visual Settings")]
    public sealed class ArenaRoomVisualSettings : ScriptableObject
    {
        [Header("Existing and Editable Detail Prefabs")]
        public SurvivalPropVisual auditoriumSeat, lockers, equipmentRack, controlDesk, scoreboard, curtainPleats, window, speaker;
        [Header("Wall Details (only on blocked tiles)")]
        [Min(2)]
        public int wallDetailSpacing = 3;
        [Range(0, 1)]
        public float detailChance = .8f;
        public Color baseboard = new Color(.20f, .24f, .30f);
        public Color capColor = new Color(.78f, .73f, .62f);
        [Min(.01f)]
        public float trimHeight = .08f;
        [Header("Court Paint and Stage Trim")]
        public Color courtKeyColor = new Color(.23f, .37f, .56f, .45f);
        public Color awayKeyColor = new Color(.66f, .13f, .15f, .75f);
        public Color stageEdge = new Color(.18f, .1f, .06f);
        [Min(0)]
        public float stageEdgeHeight = .07f;
        [Header("Gym Wall Mounts")]
        [Min(.1f)]
        public float gymBackWallHeight = 1.15f;
        public Vector2 windowWallInset = new Vector2(0, -.46f);
        [Min(0)]
        public float windowMountHeight = .28f;
        public Vector2 scoreboardWallInset = new Vector2(-.46f, 0);
        [Min(0)]
        public float scoreboardMountHeight = .30f;
        [Range(0, 3)]
        public int scoreboardFacing = 3;
        [Header("Display")]
        public bool showScoreboardText = false;
        public Font displayFont;
        public string scoreboardTitle = "GYM";
        public string scoreboardText = "GYM";
        [Min(.01f)]
        public float scoreboardTextSize = .09f;
        [Min(8)]
        public int scoreboardFontSize = 64;
        public FontStyle scoreboardFontStyle = FontStyle.Bold;
        public Color scoreboardTextColor = new Color(1, .9f, .6f);
        public Vector2 scoreboardTextInset = new Vector2(-.14f, 0);
        [Min(0)]
        public float scoreboardTextHeight = .64f;
        [Min(0)] public int scoreboardSortingOffset = 35;
        [Header("Decorative Scoreboard Readout")]
        public string scoreboardHomeLabel = "HOME", scoreboardAwayLabel = "AWAY";
        [Range(0, 99)]
        public int scoreboardHomeScore, scoreboardAwayScore;
        public string scoreboardPeriodLabel = "PERIOD";
        [Range(1, 9)]
        public int scoreboardPeriod = 1;
        [Min(0)]
        public float scoreboardColumnOffset = .82f;
        public float scoreboardHeaderHeight = 1.35f, scoreboardTeamHeight = 1.05f, scoreboardDigitsHeight = .66f, scoreboardPeriodHeight = .29f;
        [Min(.01f)]
        public float scoreboardHeaderSize = .032f, scoreboardTeamSize = .026f, scoreboardDigitsSize = .08f, scoreboardPeriodSize = .026f;
        public Color scoreboardLabelColor = new Color(.9f, .94f, 1);
        [Header("Character Readability")]
        [Min(5)]
        public int playerSortingOffset = 60;
    }
}
