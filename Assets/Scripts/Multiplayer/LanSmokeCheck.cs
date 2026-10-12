#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Escape4Now.Map;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Escape4Now.Multiplayer
{
    //Only runs when a development test player receives an explicit command-line test flag.
    public sealed class LanSmokeCheck : MonoBehaviour
    {
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lanCheck") < 0) return;
            new GameObject("LAN Check").AddComponent<LanSmokeCheck>();
        }

        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 120f;
            Application.targetFrameRate = 30;
            yield return null;
            string[] args = Environment.GetCommandLineArgs();
            string role = args[Array.IndexOf(args, "-lanCheck") + 1];
            LanMultiplayerSession lan = LanMultiplayerSession.Instance;
            bool host = role == "host";
            if (host)
            {
                //Use a repeatable Storage room when checking its reward across computers.
                if (Array.IndexOf(args, "-specialRoom") >= 0)
                    FindFirstObjectByType<IsometricMapTemplate>().UseNetworkLayout(17);
                Require(lan.StartHost(4), "Host opens lobby");
                Debug.Log("LAN_CHECK_CODE=" + lan.RoomCode);
                while (lan.ConnectedPlayers < 4) yield return null;
                Require(lan.StartHostedMatch(), "Four connected players can start");
            }
            else Require(lan.Join(args[Array.IndexOf(args, "-roomCode") + 1]), "Client begins joining by code");
            while (lan.LatestState == null) yield return null;
            LanSnapshot first = lan.LatestState;
            Require(first.players.Length == 4, "All four players received");
            Require(first.objects.Count(item => item.kind == 0) >= 7 && first.objects.Count(item => item.kind == 0) <= 13, "Approved event pool synchronized");
            Require(first.objects.Count(item => item.kind == 1) <= 3, "Sparse hallway lockers synchronized");
            IsometricMapTemplate initialMap = FindFirstObjectByType<IsometricMapTemplate>();
            //Make the optional switch available for this development-only network test.
            if (host && !first.objects.Any(item => item.kind == 0 && item.type == (int)MapEventType.LocationSwitch))
            {
                var controller = initialMap.GetComponent<MapEventController>();
                var tiles = (System.Collections.Generic.Dictionary<Vector2Int, MapEventType>)typeof(MapEventController)
                    .GetField("events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
                Vector2Int cell = initialMap.GetAvailableFloorTiles().First(pos => !initialMap.IsOccupied(pos)
                    && !initialMap.IsExit(pos) && !initialMap.IsSpecialItemTile(pos) && !controller.HasEvent(pos)
                    && !first.objects.Any(item => item.x == pos.x && item.y == pos.y));
                var marker = Instantiate(controller.GetNetworkPrefab((int)MapEventType.LocationSwitch), initialMap.GridToWorld(cell), Quaternion.identity, initialMap.transform);
                marker.Configure(initialMap.TileWidth, initialMap.TileHeight);
                tiles.Add(cell, MapEventType.LocationSwitch);
            }
            //The three usable items and one key stay unique across floor and storage.
            int floorItems = first.objects.Count(item => item.kind == 2);
            int storedItems = first.objects.Count(item => (item.kind == 1 || item.kind == 3) && item.interactable);
            Require(floorItems + storedItems == 4, "Three unique items and one key synchronized across floors and containers");
            Require(first.objects.Where(item => item.kind == 2).Select(item => item.type).Distinct().Count() == floorItems,
                "Floor item types do not repeat");
            if (Array.IndexOf(args, "-specialRoom") >= 0)
                Require(initialMap.SpecialItemCells.Count == 1, "Special room exists in this test");
            foreach (int cell in initialMap.SpecialItemCells)
                Require(first.objects.Count(item => item.kind == 2 && item.x == cell % initialMap.Width
                    && item.y == cell / initialMap.Width) == 1, "Special room reward position synchronized");
            SchoolFurniture[] furniture = FindObjectsByType<SchoolFurniture>(FindObjectsSortMode.None);
            Require(furniture.Length >= 10 && furniture.Length <= 28, "Obstacle prefabs loaded on this computer");
            Require(first.objects.Count(item => item.kind == 3) == furniture.Length, "Furniture storage is synchronized");
            Require(first.players.Select(player => initialMap.GetRoomType(new Vector2Int(player.x, player.y))).Distinct().Count() == 4,
                "Each player starts in a different room");
            foreach (SchoolFurniture item in furniture)
            {
                Require(initialMap.IsBlocked(item.Cell) && !first.players.Any(player => player.x == item.Cell.x && player.y == item.Cell.y),
                    "Furniture and player starts do not overlap");
                var data = first.objects.Single(entry => entry.kind == 3 && entry.x == item.Cell.x && entry.y == item.Cell.y);
                Require(item.GetComponent<Escape4Now.Obstacles.ObstacleStorage>().HasItem == data.interactable,
                    "Furniture contents match the host");
            }
            Debug.Log("LAN_CHECK_FURNITURE=" + string.Join(";", furniture.OrderBy(item => item.Cell.x).ThenBy(item => item.Cell.y)
                .Select(item => item.Kind + ":" + item.Cell.x + "," + item.Cell.y)));
            Debug.Log("LAN_CHECK_MAP=" + first.seed + ":" + first.exitX + "," + first.exitY);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CaptureMap(lan.LocalPlayerIndex);
            bool acted = false;
            bool testedSwitch = false;
            bool sawSwitch = false;
            bool testedKey = false;
            bool sawSharedTile = false;
            bool sawKeyBattle = false;
            while (!lan.LatestState.players.Any(player => player.won))
            {
                LanSnapshot state = lan.LatestState;
                if (state.players[0].x == state.players[1].x && state.players[0].y == state.players[1].y)
                    sawSharedTile = true;
                if (!string.IsNullOrEmpty(state.battleMessage))
                {
                    Require(state.players[0].hasKey && !state.players[1].hasKey, "Battle key ownership reaches every computer");
                    sawKeyBattle = true;
                }
                if (!sawSwitch && state.message.Contains("Location Switch: everyone swapped"))
                {
                    Require(state.players[1].protectedFromPlayers && FindFirstObjectByType<LocalMultiplayerSession>()
                        .Participants[1].EventState.IsProtectedFromPlayers, "Protection is synchronized and does not block the global swap");
                    Require(state.players.Select(player => new Vector2Int(player.x, player.y)).Distinct().Count() == 4,
                        "Swapped network players stay on separate tiles");
                    foreach (LanPlayerState player in state.players)
                        Require(initialMap.IsOccupied(new Vector2Int(player.x, player.y)), "Network swap preserves every occupied tile");
                    sawSwitch = true;
                }
                if (host && state.round >= 2)
                {
                    var session = FindFirstObjectByType<LocalMultiplayerSession>();
                    if (!testedSwitch)
                    {
                        Require(session.Participants[1].RecordSuccessfulTargetedAction(session.Participants[0]),
                            "Host records temporary direct-action protection");
                        LanObjectState marker = state.objects.Single(item => item.kind == 0 && item.type == (int)MapEventType.LocationSwitch);
                        var switchCell = new Vector2Int(marker.x, marker.y);
                        bool ready = false;
                        foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        {
                            Vector2Int cell = switchCell + step;
                            if (!initialMap.IsWalkable(cell) || initialMap.IsOccupied(cell) || initialMap.IsExit(cell)) continue;
                            ready = session.Participants[0].SetGridPosition(cell);
                            if (ready) break;
                        }
                        Require(ready, "Host approaches the switch tile");
                        session.Participants[0].SetMovesFromItem(6);
                        lan.Submit("move", switchCell.x, switchCell.y);
                        while (session.Participants[0].IsMoving) yield return null;
                        while (!lan.LatestState.message.Contains("Location Switch: everyone swapped")) yield return null;
                        Require(session.Participants[0].MovesRemaining == 5, "Network switch preserves active movement");
                        testedSwitch = true;
                        yield return new WaitForSecondsRealtime(1f);
                        continue;
                    }
                    if (!testedKey)
                    {
                        var locker = FindObjectsByType<Escape4Now.Obstacles.ObstacleStorage>(FindObjectsSortMode.None)
                            .Single(storage => storage.StoredItemPrefab is Escape4Now.Items.KeyItem);
                        var cell = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
                            .Select(step => locker.GridPosition + step).First(pos => initialMap.IsWalkable(pos) && !initialMap.IsExit(pos));
                        Require(session.Participants[0].SetGridPosition(cell), "Host approaches the key locker");
                        lan.Submit("take", locker.GridPosition.x, locker.GridPosition.y);
                        Require(session.Participants[0].Inventory.HasKey(), "The host request collects the guaranteed key");
                        var key = session.Participants[0].Inventory.KeyItems[0];
                        session.Participants[0].Inventory.RemoveKey();
                        session.Participants[1].Inventory.GiveKey(key);
                        session.Participants[1].EventState.BeginTurn();
                        session.Participants[1].SetGridPosition(cell);
                        //Choose a repeatable winning roll so the same key can test the exit next.
                        for (int seed = 0; seed < 10000; seed++)
                        {
                            UnityEngine.Random.InitState(seed);
                            if (UnityEngine.Random.Range(1, 7) <= UnityEngine.Random.Range(1, 7)) continue;
                            UnityEngine.Random.InitState(seed);
                            break;
                        }
                        lan.Submit("battle", value: 1);
                        while (string.IsNullOrEmpty(lan.LatestState.battleMessage)) yield return null;
                        yield return new WaitForSecondsRealtime(1f);
                        sawSharedTile = sawKeyBattle = true;
                        lan.Submit("battleClose");
                        Require(session.Participants[0].Inventory.HasKey(), "The shared-tile battle gives the challenger the key");
                        testedKey = true;
                        yield return null;
                        continue;
                    }
                    var approach = new Vector2Int(Mathf.Clamp(state.exitX, 1, SchoolMapLayout.Size - 2), Mathf.Clamp(state.exitY, 1, SchoolMapLayout.Size - 2));
                    Require(session.Participants[0].SetGridPosition(approach), "Host approaches exit");
                    session.Participants[0].SetMovesFromItem(1);
                    Require(session.Participants[0].MoveToGridPosition(new Vector2Int(state.exitX, state.exitY)), "Host walks into exit");
                    while (!session.Participants[0].HasReachedExit) yield return null;
                    while (!lan.LatestState.players[0].won) yield return null;
                }
                else if (!acted && lan.IsMyTurn)
                {
                    lan.Submit("roll");
                    while (!lan.LatestState.players[lan.LocalPlayerIndex].rolled) yield return null;
                    LanPlayerState player = lan.LatestState.players[lan.LocalPlayerIndex];
                    Require(player.roll >= 1 && player.roll <= 6, "Host provided a valid dice roll");
                    IsometricMapTemplate map = FindFirstObjectByType<IsometricMapTemplate>();
                    var before = new Vector2Int(player.x, player.y);
                    Vector2Int next = before;
                    foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        Vector2Int candidate = before + step;
                        if (map.IsWalkable(candidate) && !map.IsOccupied(candidate)
                            && !state.objects.Any(item => item.kind == 0 && item.x == candidate.x && item.y == candidate.y))
                        { next = candidate; break; }
                    }
                    Require(next != before, "One clear movement tile exists");
                    lan.Submit("move", next.x, next.y);
                    while (lan.LatestState.players[lan.LocalPlayerIndex].x != next.x
                        || lan.LatestState.players[lan.LocalPlayerIndex].y != next.y
                        || lan.LatestState.players[lan.LocalPlayerIndex].moving) yield return null;
                    Require(map.WorldToGrid(FindFirstObjectByType<LocalMultiplayerSession>().Participants[lan.LocalPlayerIndex].transform.position) == next,
                        "Local view matches the host's move");
                    if (lan.IsMyTurn) lan.Submit("end");
                    acted = true;
                }
                yield return null;
            }
            Require(sawSwitch, "Location Switch reached this computer");
            Require(sawSharedTile && sawKeyBattle, "Shared tiles and key battles reached this computer");
            Require(lan.LatestState.players[0].won, "Exit result synchronized");
            Debug.Log("LAN MULTI-PROCESS CHECK PASSED: " + role + " player " + (lan.LocalPlayerIndex + 1));
            //Let the host publish its final state before the test process closes.
            yield return new WaitForSecondsRealtime(host ? 3f : 1f);
            Application.Quit(0);
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup > deadline)
            { Debug.LogError("LAN CHECK TIMED OUT"); Application.Quit(1); }
        }

        //Saves the actual host or client camera to check that both render their map and objects.
        private static void CaptureMap(int player)
        {
            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                target.Create();
                camera.targetTexture = target;
                UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>().SendMessage("FitCamera");
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Require(image.GetPixels32().Distinct().Take(11).Count() > 10, "Camera is not blank");
                System.IO.File.WriteAllBytes("Logs/lan-map-player-" + player + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = oldActive;
                target.Release();
                Destroy(target);
                Destroy(image);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) { Debug.LogError("FAIL: " + message); Application.Quit(1); throw new Exception(message); }
            Debug.Log("PASS: " + message);
        }
    }
}
#endif
