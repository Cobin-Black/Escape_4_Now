using System.Collections.Generic;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Map
{
    //Lists the five kinds of event tiles.
    public enum MapEventType { Warp, RandomEvent, Freeze, SpeedBoost, LuckyRoll }

    //Places event tiles and applies one effect when a player lands on them.
    [RequireComponent(typeof(IsometricMapTemplate))]
    public sealed class MapEventController : MonoBehaviour
    {
        [System.Serializable]
        public sealed class EventTile
        {
            public Vector2Int position;
            public MapEventType type;
        }

        //Each event uses one open tile. Invalid or repeated positions are ignored.
        [SerializeField] private EventTile[] tiles =
        {
            new EventTile { position = new Vector2Int(4, 2), type = MapEventType.Warp },
            new EventTile { position = new Vector2Int(6, 4), type = MapEventType.RandomEvent },
            new EventTile { position = new Vector2Int(8, 6), type = MapEventType.Freeze },
            new EventTile { position = new Vector2Int(4, 8), type = MapEventType.SpeedBoost },
            new EventTile { position = new Vector2Int(10, 10), type = MapEventType.LuckyRoll }
        };

        private IsometricMapTemplate map;
        private readonly Dictionary<Vector2Int, MapEventType> events = new Dictionary<Vector2Int, MapEventType>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Material material;
        private string message = "";

        //Checks the event positions before movement starts.
        private void Awake()
        {
            map = GetComponent<IsometricMapTemplate>();
            if (tiles == null) return;
            for (int i = 0; i < Mathf.Min(tiles.Length, map.Width * map.Height); i++)
            {
                EventTile tile = tiles[i];
                if (tile == null || !map.IsWalkable(tile.position) || map.IsExit(tile.position)
                    || events.ContainsKey(tile.position) || !System.Enum.IsDefined(typeof(MapEventType), tile.type)) continue;
                events.Add(tile.position, tile.type);
            }
        }

        //Adds a colored diamond and a short name to each event tile.
        private void Start()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            material = new Material(shader);
            foreach (KeyValuePair<Vector2Int, MapEventType> tile in events)
            {
                CreateMarker(tile.Key, tile.Value);
            }
        }

        //Draws the most recent event result below the dice display.
        private void OnGUI()
        {
            GUI.Label(new Rect(16f, 140f, Mathf.Max(0f, Screen.width - 32f), 65f), message);
        }

        //Shows the effect received by a player.
        public void ShowMessage(PlayerCharacter player, string result)
        {
            if (player != null) message = player.name + ": " + result;
        }

        //Applies only the destination event, without triggering events after forced movement.
        public void ResolveLanding(PlayerCharacter player, Vector2Int direction)
        {
            if (player == null || player.IsMoving || map == null
                || !map.IsWalkable(player.GridPosition) || map.IsExit(player.GridPosition)
                || !events.TryGetValue(player.GridPosition, out MapEventType type)) return;

            switch (type)
            {
                case MapEventType.Warp:
                    Warp(player);
                    break;
                case MapEventType.RandomEvent:
                    int result = Random.Range(0, 4);
                    if (result == 0 || result == 1)
                    {
                        MoveExtra(player, result == 0 ? direction : -direction, result == 0);
                    }
                    else if (result == 2)
                    {
                        player.EventState.GiveLuckyRoll();
                        ShowMessage(player, "Random Event: Lucky Roll next turn.");
                    }
                    else
                    {
                        player.EventState.GiveFreeze();
                        ShowMessage(player, "Random Event: Freeze. The next turn will be skipped.");
                    }
                    break;
                case MapEventType.Freeze:
                    player.EventState.GiveFreeze();
                    ShowMessage(player, "Freeze. The next turn will be skipped.");
                    break;
                case MapEventType.SpeedBoost:
                    player.EventState.GiveSpeedBoost();
                    ShowMessage(player, "Speed Boost: +2 movement next turn.");
                    break;
                case MapEventType.LuckyRoll:
                    player.EventState.GiveLuckyRoll();
                    ShowMessage(player, "Lucky Roll: roll twice next turn and keep the higher roll.");
                    break;
            }
        }

        //Chooses from a finite list of safe tiles, so the search cannot loop forever.
        private void Warp(PlayerCharacter player)
        {
            List<Vector2Int> choices = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (IsWarpDestination(cell)
                        && !player.IsOccupiedByOtherPlayer(cell)) choices.Add(cell);
                }
            }

            if (choices.Count == 0)
            {
                ShowMessage(player, "Warp: no safe destination. Turn ended.");
                return;
            }

            bool moved = player.SetGridPosition(choices[Random.Range(0, choices.Count)]);
            ShowMessage(player, moved ? "Warp: teleported. Turn ended." : "Warp: destination unavailable. Turn ended.");
        }

        //Rejects walls, exits, and event tiles before choosing a warp destination.
        private bool IsWarpDestination(Vector2Int cell)
        {
            return map.IsWalkable(cell) && !map.IsExit(cell) && !events.ContainsKey(cell);
        }

        //Moves at most two spaces in a straight line and stops before a blocked tile.
        private void MoveExtra(PlayerCharacter player, Vector2Int direction, bool forward)
        {
            if (direction != Vector2Int.up && direction != Vector2Int.down
                && direction != Vector2Int.left && direction != Vector2Int.right) return;

            int moved = 0;
            for (int i = 0; i < 2; i++)
            {
                Vector2Int next = player.GridPosition + direction;
                if (!map.IsWalkable(next) || player.IsOccupiedByOtherPlayer(next)
                    || !player.SetGridPosition(next)) break;
                moved++;
                if (map.IsExit(next)) break;
            }
            string effect = forward ? "Move Forward" : "Move Backward";
            ShowMessage(player, "Random Event: " + effect + ". Moved " + moved + " spaces.");
        }

        //Draws a small marker above the existing floor without changing the map mesh.
        private void CreateMarker(Vector2Int cell, MapEventType type)
        {
            Color[] colors = { new Color(0.7f, 0.3f, 0.8f), new Color(0.9f, 0.55f, 0.2f),
                new Color(0.3f, 0.75f, 0.95f), new Color(0.95f, 0.8f, 0.2f), new Color(0.25f, 0.8f, 0.4f) };
            string[] labels = { "Warp", "Random", "Freeze", "Speed +2", "Lucky" };
            float halfWidth = map.TileWidth * 0.42f;
            float halfHeight = map.TileHeight * 0.42f;
            Mesh mesh = new Mesh
            {
                vertices = new[] { new Vector3(0, halfHeight), new Vector3(halfWidth, 0),
                    new Vector3(0, -halfHeight), new Vector3(-halfWidth, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
                colors = new[] { colors[(int)type], colors[(int)type], colors[(int)type], colors[(int)type] }
            };
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            GameObject marker = new GameObject("Event " + labels[(int)type]);
            marker.transform.SetParent(transform, false);
            marker.transform.position = map.GridToWorld(cell);
            marker.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -900;

            GameObject label = new GameObject("Event label");
            label.transform.SetParent(marker.transform, false);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = labels[(int)type];
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.black;
            label.GetComponent<MeshRenderer>().sortingOrder = -899;
        }

        //Releases only the drawing data created by this component.
        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes)
            {
                if (mesh != null) Destroy(mesh);
            }
            if (material != null) Destroy(material);
        }
    }
}
