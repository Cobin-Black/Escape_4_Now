using System;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    public enum ArenaRoom
    {
        Gymnasium,
        Auditorium
    }

    public enum ArenaSurface
    {
        Wood,
        Court,
        Carpet,
        Service,
        Stage
    }

    [CreateAssetMenu(menuName = "Escape 4 Now/Arena/Map Settings")]
    public sealed class ArenaMapSettings : ScriptableObject
    {
        [Header("Room and Randomness")]
        public ArenaRoom room;
        public Vector2Int size = new Vector2Int(20, 20);
        public bool fixedSeed;
        public int seed = 12345;
        [Min(1)]
        public int generationAttempts = 20;
        [Header("Room Layout")]
        [Min(3)]
        public int sideRoomDepth = 3;
        [Min(2)]
        public int aisleWidth = 2;
        [Min(3)]
        public int stageDepth = 4;
        [Min(2)]
        public int seatingBlockWidth = 4;
        [Min(1)]
        public int seatingRowSpacing = 1;
        [Min(1)]
        public int seatingRowsPerSection = 2;
        [Header("Safe Player Spawns")]
        [Range(.1f, .45f)]
        public float playerRadius = .24f;
        [Min(1)]
        public float minimumSpawnDistance = 4.5f;
        [Min(1)]
        public int minimumOpenNeighbours = 3;
        [Min(1)]
        public int spawnAttempts = 300;
        [Header("Controlled Equipment Placement")]
        public Vector2Int equipmentCount = new Vector2Int(4, 7);
        [Min(0)]
        public int routeClearance = 2;
        public SurvivalPropVisual[] equipment = Array.Empty<SurvivalPropVisual>();
        public SurvivalPropVisual bench, bleachers, hoop, crates;
        public ArenaRoomVisualSettings roomVisuals;
        public ArenaObstacleSettings obstacleGroups;
        [Header("Room Labels")]
        public bool showRoomNames = false;
        public Font labelFont;
        [Min(.01f)]
        public float labelSize = .055f;
        public Color labelColor = new Color(.9f, .95f, 1, .8f);
        [Header("Materials and Colors")]
        public Material material;
        public Vector2 tileSize = new Vector2(1.2f, .6f);
        public Color wood = new Color(.73f, .51f, .27f);
        public Color court = new Color(.80f, .63f, .38f);
        public Color carpet = new Color(.40f, .19f, .26f);
        public Color service = new Color(.44f, .49f, .51f);
        public Color stage = new Color(.49f, .31f, .18f);
        public Color wall = new Color(.55f, .59f, .59f);
        public Color markings = new Color(.95f, .92f, .78f);
        [Range(0, .1f)]
        public float floorVariation = .025f;
        [Min(.001f)]
        public float floorDepth = .014f;
        [Min(.1f)]
        public float wallHeight = .55f;
        [Min(.1f)]
        public float frontWallHeight = .22f;
        [Min(.01f)]
        public float lineWidth = .035f;
        [Header("Auditorium Seating")]
        [Range(0, 3)]
        public int seatQuarterTurns = 2;
        public Color seatColor = new Color(.57f, .16f, .22f);
        public Vector3 seatSize = new Vector3(.65f, .55f, .32f);
        public Color curtainColor = new Color(.48f, .08f, .13f);
        [Min(.1f)]
        public float curtainHeight = 1.2f;
        [Header("Shared Camera")]
        public bool frameWholeMap = true;
        [Min(0)]
        public float cameraPropHeadroom = 1.4f;
        [Range(0, .15f)]
        public float cameraSpreadZoom = .025f;
        public Color background = new Color(.055f, .09f, .12f);
        [Min(1)]
        public float minimumZoom = 5;
        [Min(1)]
        public float maximumZoom = 20;
        public Vector2 cameraPadding = new Vector2(3.5f, 3);
        [Tooltip("Normalized area used to frame players. Leaves space beside the HUD.")]
        public Rect cameraSafeViewport = new Rect(.24f, .03f, .74f, .94f);
        public Vector2 cameraFocusOffset = Vector2.zero;
        [Min(.1f)]
        public float cameraResponse = 4;
        public Color SurfaceColor(ArenaSurface surface) => surface == ArenaSurface.Court ? court : surface == ArenaSurface.Carpet ? carpet : surface == ArenaSurface.Service ? service : surface == ArenaSurface.Stage ? stage : wood;
    }
}
