using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Cosmetic details stay on existing blocked tiles. They cannot reserve ground or change routes.
    public static class ArenaRoomDecor
    {
        public static void Build(ArenaMap map, Transform parent, ArenaRoomVisualSettings style)
        {
            var settings = map.Settings;
            var layout = map.Layout;
            var random = new System.Random(unchecked(map.Seed ^ 0x1F29));
            int depth = Mathf.Clamp(settings.sideRoomDepth, 3, layout.Height / 4);
            for (int x = 1; x < layout.Width - 1; x++)
                for (int y = 1; y < layout.Height - 1; y++)
                {
                    if (!layout.blocked[x, y] || layout.seats.Contains(new Vector2Int(x, y)) || layout.props.Contains(new Vector2Int(x, y)) || layout.fixtures.ContainsKey(new Vector2Int(x, y)) || layout.groupedProps.Exists(p => p.cell == new Vector2Int(x, y)))
                        continue;
                    var cell = new Vector2Int(x, y);
                    var anchor = new GameObject("Wall trim").transform;
                    anchor.SetParent(parent, false);
                    anchor.position = map.World(cell);
                    Block(map, anchor, "Painted baseboard", Vector2.zero, 1, 1, 0, style.trimHeight, style.baseboard, map.Order(cell) + 1);
                    Block(map, anchor, "Warm wall cap", Vector2.zero, 1, 1, settings.wallHeight, .025f, style.capColor, map.Order(cell) + 2);
                    if ((x + y) % Mathf.Max(2, style.wallDetailSpacing) != 0 || random.NextDouble() > style.detailChance)
                        continue;
                    if (settings.room == ArenaRoom.Auditorium && y == layout.Height - depth - 1)
                        Place(map, parent, style.curtainPleats, cell);
                }

            // The outer back wall provides space for windows and equipment, without adding walkable obstructions.
            for (int x = 2; x < layout.Width - 2; x += Mathf.Max(2, style.wallDetailSpacing))
            {
                var cell = new Vector2Int(x, layout.Height - 1);
                if (settings.room == ArenaRoom.Gymnasium)
                    Mount(map, parent, style.window, cell, style.windowWallInset, style.windowMountHeight, 0);
                else
                    Place(map, parent, style.speaker, cell);
            }

            for (int x = 2; x < layout.Width - 2; x += Mathf.Max(2, style.wallDetailSpacing))
            {
                var cell = new Vector2Int(x, settings.room == ArenaRoom.Gymnasium ? depth : 0);
                Place(map, parent, settings.room == ArenaRoom.Gymnasium ? style.lockers : style.controlDesk, cell, 2);
            }

            if (settings.room == ArenaRoom.Gymnasium)
            {
                var boardCell = new Vector2Int(layout.Width - 1, layout.Height / 2);
                var board = Mount(map, parent, style.scoreboard, boardCell, style.scoreboardWallInset, style.scoreboardMountHeight, style.scoreboardFacing);
                // The wide board spans several wall tiles; keep its backing in front of those tiles too.
                var surface=board!=null?board.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>():null;
                if(surface!=null) surface.sortingOrder=map.Order(boardCell)+style.scoreboardSortingOffset;
                if (board != null && style.showScoreboardText && style.displayFont != null)
                {
                    // Separate labels and number windows stay readable without adding gameplay scoring.
                    ScoreText(map, board, boardCell, style, style.scoreboardTitle, 0, style.scoreboardHeaderHeight, style.scoreboardHeaderSize, style.scoreboardLabelColor);
                    ScoreText(map, board, boardCell, style, style.scoreboardHomeLabel, -style.scoreboardColumnOffset, style.scoreboardTeamHeight, style.scoreboardTeamSize, style.scoreboardLabelColor);
                    ScoreText(map, board, boardCell, style, style.scoreboardAwayLabel, style.scoreboardColumnOffset, style.scoreboardTeamHeight, style.scoreboardTeamSize, style.scoreboardLabelColor);
                    ScoreText(map, board, boardCell, style, style.scoreboardHomeScore.ToString("00"), -style.scoreboardColumnOffset, style.scoreboardDigitsHeight, style.scoreboardDigitsSize, style.scoreboardTextColor);
                    ScoreText(map, board, boardCell, style, style.scoreboardAwayScore.ToString("00"), style.scoreboardColumnOffset, style.scoreboardDigitsHeight, style.scoreboardDigitsSize, style.scoreboardTextColor);
                    ScoreText(map, board, boardCell, style, style.scoreboardPeriodLabel + " " + style.scoreboardPeriod, 0, style.scoreboardPeriodHeight, style.scoreboardPeriodSize, style.scoreboardLabelColor);
                }

                float middle = (layout.Height - 1) * .5f;
                foreach (float x in new[]
                {
                    5f,
                    layout.Width - 6f
                }

                )
                {
                    var anchor = new GameObject("Painted basketball key").transform;
                    anchor.SetParent(parent, false);
                    anchor.position = map.World(new Vector2(x, middle));
                    Block(map, anchor, "Team color paint", Vector2.zero, 2.8f, 4, .002f, .002f, x < layout.Width / 2 ? style.courtKeyColor : style.awayKeyColor, -950);
                }
            }
            else
            {
                float start = layout.Height - depth - Mathf.Clamp(settings.stageDepth, 3, 7) - .5f;
                var anchor = new GameObject("Stage front trim").transform;
                anchor.SetParent(parent, false);
                anchor.position = map.World(new Vector2((layout.Width - 1) * .5f, start));
                Block(map, anchor, "Low wood stage edge", Vector2.zero, layout.Width - 4, .12f, 0, style.stageEdgeHeight, style.stageEdge, -900);
            }
        }

        private static void ScoreText(ArenaMap map, Transform board, Vector2Int cell, ArenaRoomVisualSettings style, string value, float column, float height, float size, Color color)
        {
            var label = new GameObject("Scoreboard display").AddComponent<TextMesh>();
            label.transform.SetParent(board, false);
            Vector2 axis = style.scoreboardFacing == 0 ? Vector2.right : style.scoreboardFacing == 1 ? Vector2.up : style.scoreboardFacing == 2 ? Vector2.left : Vector2.down;
            label.transform.position = map.World((Vector2)cell + style.scoreboardWallInset + style.scoreboardTextInset + axis * column) + Vector3.up * (style.scoreboardMountHeight + height);
            Vector2 baseline = map.ScreenDirection(axis);
            float angle = Mathf.Atan2(baseline.y, baseline.x) * Mathf.Rad2Deg;
            if (angle > 90)
                angle -= 180;
            else if (angle < -90)
                angle += 180;
            label.transform.rotation = Quaternion.Euler(0, 0, angle);
            label.font = style.displayFont;
            label.fontSize = style.scoreboardFontSize;
            label.fontStyle = style.scoreboardFontStyle;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.text = value;
            label.color = color;
            label.GetComponent<Renderer>().sharedMaterial = style.displayFont.material;
            label.GetComponent<Renderer>().sortingOrder = map.Order(cell) + style.scoreboardSortingOffset + 1;
        }

        // Mount against the inner face of an existing wall; this never reserves playable floor.
        private static Transform Mount(ArenaMap map, Transform parent, SurvivalPropVisual prefab, Vector2Int cell, Vector2 inset, float height, int turns)
        {
            var prop = Place(map, parent, prefab, cell, turns);
            if (prop != null)
                prop.position = map.World((Vector2)cell + inset) + Vector3.up * height;
            return prop;
        }

        private static Transform Place(ArenaMap map, Transform parent, SurvivalPropVisual prefab, Vector2Int cell, int turns = 0)
        {
            if (prefab == null || map.Layout.Walkable(cell))
                return null;
            var prop = Object.Instantiate(prefab, parent);
            prop.transform.position = map.World(cell);
            prop.Build(map.Settings.tileSize, Vector2.one * .88f, map.Settings.material, map.Order(cell) + 6, true, turns);
            return prop.transform;
        }

        private static void Block(ArenaMap map, Transform parent, string label, Vector2 offset, float width, float depth, float bottom, float height, Color color, int order)
        {
            var renderer = IsometricMapTemplate.CreateBlock(parent, label, offset, width, depth, bottom, height, color, order, map.Settings.tileSize.x, map.Settings.tileSize.y, map.Settings.material);
            var cleanup = renderer.gameObject.AddComponent<ArenaGeneratedMesh>();
            cleanup.Own(renderer.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
