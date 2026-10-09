using System;
using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//Checks same-device setup and movement without saving changes to the scene.
[InitializeOnLoad]
public static class LocalMultiplayerChecks
{
    private const string Running = "LocalMultiplayerChecks.Running";
    private static IEnumerator checks;
    private static Keyboard keyboard;
    private static double deadline;
    private static InputSettings.BackgroundBehavior previousBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

    //Restores the test runner after Unity reloads scripts for Play Mode.
    static LocalMultiplayerChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                checks = Array.IndexOf(Environment.GetCommandLineArgs(), "-lanAuthority") >= 0
                    ? LanAuthorityChecks.Run() : CheckMatch();
                deadline = EditorApplication.timeSinceStartup + 90;
                previousBackground = InputSystem.settings.backgroundBehavior;
                previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                EditorApplication.update += Tick;
                UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>().StartCoroutine(RunChecks());
            }
        };
    }

    //Run in a separate editor with -executeMethod LocalMultiplayerChecks.Run.
    public static void Run()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    //Prevents a broken test from waiting forever.
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Checks timed out.");
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    //Runs input checks on game frames, not Editor UI updates.
    private static IEnumerator RunChecks()
    {
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            if (!more) { Finish(0); yield break; }
            yield return checks.Current;
        }
    }

    //Tests player ownership, valid spawns, connected paths, and a complete round.
    private static IEnumerator CheckMatch()
    {
        yield return null;
        LocalMultiplayerSession session = UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>();
        Require(session != null, "Scene has the multiplayer component");
        int count = 4;
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
            if (arguments[i] == "-schoolPlayers" && int.TryParse(arguments[i + 1], out int chosen)) count = chosen;
        //Select a generated layout with the requested optional room for focused tests.
        for (int i = 0; i < arguments.Length - 1; i++)
            if (arguments[i] == "-specialRoom" && Enum.TryParse(arguments[i + 1], out SchoolSpaceType type))
            {
                bool found = false;
                for (int seed = 0; seed < 10000 && !found; seed++)
                    foreach (SchoolSecondarySpace space in new SchoolMapLayout(seed).SecondarySpaces)
                        if (space.Type == type)
                        {
                            UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>().UseNetworkLayout(seed);
                            found = true; Debug.Log("SPECIAL ROOM SEED: " + seed); break;
                        }
                Require(found, "Optional room test seed exists");
            }
        Require(!session.TryStartLocalMatch(5), "More than four players are rejected");
        Require(!session.TryStartLocalMatch(1), "At least two players are needed");
        Require(session.TryStartLocalMatch(count), "Selected player count can start");
        Require(!session.TryStartLocalMatch(4), "A running match cannot duplicate players");
        for (int i = 0; i < 10; i++) yield return null;
        PlayerCharacter[] players = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        Array.Sort(players, (left, right) => string.CompareOrdinal(left.name, right.name));
        TurnSystemController turns = UnityEngine.Object.FindFirstObjectByType<TurnSystemController>();
        IsometricMapTemplate map = UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>();
        Require(players.Length == count && map.Width == 25 && map.Height == 25, "Selected players on an 25 by 25 map");
        int doorCount = 0;
        int exitCount = 0;
        Vector2Int exitCell = default;
        foreach (Transform child in map.GetComponentsInChildren<Transform>())
        {
            if (child.name == "Exit door")
            {
                exitCount++;
                Vector2Int cell = map.WorldToGrid(child.parent.position);
                exitCell = cell;
                Require(child.parent.Find("Wall") == null && child.parent.Find("Wall cap") == null,
                    "Exit door replaces the border wall block");
                Require((cell.x == 0 || cell.x == SchoolMapLayout.Size - 1) != (cell.y == 0 || cell.y == SchoolMapLayout.Size - 1),
                    "Exit door is on a border, not a corner");
                Vector2Int inside = new Vector2Int(Mathf.Clamp(cell.x, 1, SchoolMapLayout.Size - 2), Mathf.Clamp(cell.y, 1, SchoolMapLayout.Size - 2));
                Require(map.IsWalkable(inside) && !map.IsOccupied(inside), "Exit has a clear floor approach");
                Require(map.IsExit(cell) && map.IsWalkable(cell), "Door uses the existing exit tile");
            }
            if (child.name != "Door frame") continue;
            doorCount++;
            Require(child.GetComponent<Collider>() == null && child.GetComponent<Collider2D>() == null,
                "Visual door has no blocking collider");
        }
        Require(doorCount >= 3, "Connected rooms have visible entrances");
        Require(exitCount == 1, "One working exit door is present");
        MapEventController events = UnityEngine.Object.FindFirstObjectByType<MapEventController>();
        Require(events != null && events.isActiveAndEnabled && events.EventsPlaced,
            "Existing event spawner is active");
        int eventCount = UnityEngine.Object.FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None).Length;
        Require(eventCount >= 7 && eventCount <= 13, "Only the approved event pool is placed");
        var eventTypes = new HashSet<MapEventType>();
        var eventCells = new HashSet<Vector2Int>();
        foreach (EventTileMarker marker in UnityEngine.Object.FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None))
        {
            Require(eventTypes.Add(marker.EventType), "Each event type appears at most once");
            Require(eventCells.Add(map.WorldToGrid(marker.transform.position)), "Event tiles do not overlap");
        }
        //A second event with a different object ID must still be rejected if its type repeats.
        LanSnapshot snapshot = new LanWorldView(session).Capture();
        Require(LanWorldView.IsValid(snapshot), "Normal event snapshot is valid");
        LanObjectState firstEvent = Array.Find(snapshot.objects, item => item.kind == 0);
        Array.Resize(ref snapshot.objects, snapshot.objects.Length + 1);
        snapshot.objects[snapshot.objects.Length - 1] = new LanObjectState
            { id = 0, kind = 0, type = firstEvent.type, x = (firstEvent.x + 1) % map.Width, y = firstEvent.y };
        Require(!LanWorldView.IsValid(snapshot), "Network rejects a duplicated event type on another tile");
        int missingType = 0;
        while (eventTypes.Contains((MapEventType)missingType)) missingType++;
        if (missingType <= (int)MapEventType.GoodLuckDay)
        {
            snapshot.objects[snapshot.objects.Length - 1].type = missingType;
            snapshot.objects[snapshot.objects.Length - 1].x = firstEvent.x;
            Require(!LanWorldView.IsValid(snapshot), "Network rejects overlapping event tiles");
        }
        Require(UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None).Length >= 2,
            "Existing items are present");
        var lockers = UnityEngine.Object.FindObjectsByType<Escape4Now.Obstacles.ObstacleGridControl>(FindObjectsSortMode.None);
        Require(lockers.Length <= 3, "Only a few lockers use safe hallway positions");
        foreach (var locker in lockers)
        {
            Require(map.IsLockerPosition(locker.GridPosition) && locker.GetComponent<Escape4Now.Obstacles.ObstacleStorage>() != null,
                "Lockers stay beside hallway walls and support storage");
            Require(locker.GetComponent<MeshFilter>().sharedMesh.vertexCount > 12,
                "Locker mesh includes its door and handle details");
        }
        foreach (Transform child in map.GetComponentsInChildren<Transform>())
            Require(child.name != "Room Label", "Main room names are hidden");
        var cells = new HashSet<Vector2Int>();
        var startingRooms = new HashSet<SchoolRoomType>();
        for (int i = 0; i < players.Length; i++)
        {
            Require(players[i].isActiveAndEnabled && cells.Add(players[i].GridPosition), "Unique active start");
            Require(map.IsWalkable(players[i].GridPosition) && map.IsOccupied(players[i].GridPosition), "Start is a floor tile");
            Require(map.GetRoomType(players[i].GridPosition) != SchoolRoomType.None
                && startingRooms.Add(map.GetRoomType(players[i].GridPosition)), "Players start in different main rooms");
            Require(players[i].Inventory != null && players[i].Inventory.Player == players[i], "Separate player inventory");
            if (i > 0) Require(map.TryFindPath(players[0].GridPosition, players[i].GridPosition, out _), "Room spawns are connected");
        }
        var available = map.GetAvailableFloorTiles();
        SchoolFurniture[] furniture = UnityEngine.Object.FindObjectsByType<SchoolFurniture>(FindObjectsSortMode.None);
        SchoolFurniture[] templates = Resources.LoadAll<SchoolFurniture>("SchoolObstacles");
        Require(templates.Length == 22, "All room and hallway obstacle prefab assets load");
        Require(Resources.LoadAll<EventTileMarker>("EventTiles").Length == 13, "Every event has its own prefab");
        foreach (SchoolFurniture template in templates)
            Require(PrefabUtility.IsPartOfPrefabAsset(template) && template.IsValidTemplate()
                && template.GetComponent<Escape4Now.Obstacles.ObstacleStorage>() != null, "Valid obstacle prefab with shared storage");
        Require(furniture.Length >= 10 && furniture.Length <= 28, "Required and optional obstacles stay sparse");
        foreach (int address in map.SpecialItemCells)
        {
            var cell = new Vector2Int(address % map.Width, address / map.Width);
            int rewardCount = 0;
            foreach (var reward in UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None))
                if (reward.IsAtPosition(cell)) rewardCount++;
            Require(rewardCount == 1 && !events.HasEvent(cell) && map.IsWalkable(cell)
                && map.TryFindPath(players[0].GridPosition, cell, out _), "Special room guarantees a reachable floor item");
        }
        var furnitureCells = new HashSet<Vector2Int>();
        foreach (SchoolFurniture item in furniture)
        {
            Require(furnitureCells.Add(item.Cell) && !cells.Contains(item.Cell) && map.IsBlocked(item.Cell),
                "Furniture reserves separate blocked tiles");
            Require(item.GetComponentsInChildren<MeshRenderer>().Length > 0, "Furniture prefab has visible parts");
            var storage = item.GetComponent<Escape4Now.Obstacles.ObstacleStorage>();
            Require(storage != null && storage.CanInteract && storage.GridPosition == item.Cell,
                "Every furniture object supports storage at its own tile");
            Require(Array.Exists(templates, template => template.Kind == item.Kind), "Furniture uses a registered prefab kind");
            Require(!events.HasEvent(item.Cell), "Furniture does not overlap events");
            foreach (Escape4Now.Obstacles.ObstacleGridControl obstacle in UnityEngine.Object.FindObjectsByType<Escape4Now.Obstacles.ObstacleGridControl>(FindObjectsSortMode.None))
                Require(obstacle.GridPosition != item.Cell, "Furniture does not overlap existing obstacles");
            foreach (Escape4Now.Items.Item pickup in UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None))
                Require(!pickup.IsAtPosition(item.Cell), "Furniture does not overlap map items");
        }
        foreach (Vector2Int cell in available)
            Require(map.IsWalkable(cell) && !map.IsOccupied(cell), "Placement hook returns empty floors");
        Require(!map.TryGetRandomFloor(out _, new HashSet<Vector2Int>(available)), "Full reservation fails cleanly");
        foreach (SchoolRoom room in map.Rooms)
        {
            var roomCells = map.GetAvailableRoomTiles(room.Type);
            Require(roomCells.Count > 0, "Room query returns free floor");
            foreach (Vector2Int cell in roomCells)
                Require(available.Contains(cell) && map.GetRoomType(cell) == room.Type, "Room query filters correctly");
            Require(map.GetAvailableRoomTiles(room.Type, new HashSet<Vector2Int>(roomCells)).Count == 0,
                "Room query respects reservations");
        }
        Require(map.GetAvailableRoomTiles(SchoolRoomType.None).Count == 0, "No room means no room tiles");
        Require(!ReferenceEquals(players[0].EventState, players[1].EventState), "Separate player state");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            CaptureMap(map, 1280, 720, "wide");
            CaptureMap(map, 960, 720, "standard");
        }
        //Checks that furniture gives one item only to an adjacent player on their turn.
        var container = furniture[0].GetComponent<Escape4Now.Obstacles.ObstacleStorage>();
        var savedContents = container.StoredItemPrefab;
        var testItem = map.GetComponent<Escape4Now.Items.ItemSpawner>().GetNetworkPrefab(1);
        container.SetStoredItem(testItem);
        Vector2Int savedPosition = players[0].GridPosition;
        Require(!container.TakeNetworkItem(players[1]), "Inactive players cannot search furniture");
        Require(!container.TakeNetworkItem(players[0]), "Distant players cannot search furniture");
        bool reachedContainer = false;
        foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            Vector2Int next = container.GridPosition + direction;
            if (!map.IsWalkable(next) || map.IsOccupied(next)) continue;
            reachedContainer = players[0].SetGridPosition(next);
            if (reachedContainer) break;
        }
        Require(reachedContainer && container.TakeNetworkItem(players[0]), "Adjacent player takes the stored item");
        Require(!container.HasItem && players[0].Inventory.IsFull, "Stored item transfers once");
        container.SetStoredItem(testItem);
        Require(!container.TakeNetworkItem(players[0]) && container.HasItem, "Full inventory leaves storage unchanged");
        var collected = players[0].Inventory.Items[0];
        players[0].Inventory.RemoveItem(collected);
        UnityEngine.Object.Destroy(collected.gameObject);
        container.SetStoredItem(null);
        Require(!container.TakeNetworkItem(players[0]), "Empty storage cannot create an item");
        container.SetStoredItem(savedContents);
        Require(players[0].SetGridPosition(savedPosition), "Storage test restores the player start");
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.actions.Enable();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null;
        yield return null;
        Require(players[0].HasRolled && !players[1].HasRolled, "Only the current player rolls");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(!players[1].MoveToGridPosition(players[1].GridPosition + Vector2Int.up), "Inactive player cannot move");
        Vector2Int before = players[0].GridPosition;
        Vector2Int destination = before;
        foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            if (map.IsWalkable(before + direction) && !map.IsOccupied(before + direction)
                && !events.HasEvent(before + direction))
            { destination = before + direction; break; }
        Require(destination != before && players[0].MoveToGridPosition(destination), "Active player can move");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == destination, "Move ends on the chosen floor");
        if (turns.CurrentPlayerIndex == 0) turns.AdvanceTurn();
        for (int i = 1; i < count; i++)
        {
            Require(turns.CurrentPlayerIndex == i, "Next player's turn");
            players[i].SetMovesFromItem(1);
            turns.AdvanceTurn();
        }
        Require(turns.CurrentPlayerIndex == 0 && turns.TurnNumber == 2, "Round returns to Player 1");
        IEnumerator passageChecks = CheckPlayerPassage(map, players);
        while (passageChecks.MoveNext()) yield return passageChecks.Current;
        IEnumerator switchChecks = CheckLocationSwitch(map, players, turns);
        while (switchChecks.MoveNext()) yield return switchChecks.Current;
        CheckNewEvents(map, players);
        events.ShowMessage(players[0], "Stumble: you lost 2 moves. You have 1 left.");
        Require(events.CurrentMessage.Contains(players[0].name) && events.CurrentMessage.Contains("You have 1 left"),
            "Event notice identifies the player and explains the result");
        //Check the last step through the real door after the regular turn checks.
        Vector2Int approach = new Vector2Int(Mathf.Clamp(exitCell.x, 1, SchoolMapLayout.Size - 2), Mathf.Clamp(exitCell.y, 1, SchoolMapLayout.Size - 2));
        Require(players[0].SetGridPosition(approach), "Player can stand beside the exit");
        players[0].SetMovesFromItem(1);
        Require(players[0].MoveToGridPosition(exitCell), "Player can walk through the exit");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == exitCell && Time.timeScale == 0f, "Reaching the door pauses the match for the win");
        Time.timeScale = 1f;
        Debug.Log("SAME-DEVICE AND SCHOOL MAP CHECKS PASSED");
    }

    //Checks transit through players and stopping before an occupied destination.
    private static IEnumerator CheckPlayerPassage(IsometricMapTemplate map, PlayerCharacter[] players)
    {
        var events = map.GetComponent<MapEventController>();
        var saved = new Vector2Int[players.Length];
        for (int i = 0; i < players.Length; i++) saved[i] = players[i].GridPosition;
        Vector2Int start = default, direction = default;
        bool found = false;
        foreach (Vector2Int candidate in map.GetAvailableFloorTiles())
        {
            foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.right })
            {
                bool clear = true;
                for (int i = 0; i <= 4; i++)
                {
                    Vector2Int cell = candidate + step * i;
                    if (!map.IsWalkable(cell) || map.IsOccupied(cell) || map.IsExit(cell) || events.HasEvent(cell)) clear = false;
                    foreach (var item in UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None))
                        if (item.IsAtPosition(cell)) clear = false;
                }
                if (!clear) continue;
                start = candidate; direction = step; found = true; break;
            }
            if (found) break;
        }
        Require(found, "Clear test route exists");
        for (int i = 0; i < players.Length; i++) Require(players[i].SetGridPosition(start + direction * i), "Place test players in a row");
        players[0].SetMovesFromItem(6);
        Require(players[0].MoveToGridPosition(start + direction * 4), "Route passes through other players");
        bool clearStops = true;
        while (players[0].IsMoving)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (PlayerCharacter player in players)
                clearStops &= occupied.Add(player.GridPosition) && map.IsOccupied(player.GridPosition);
            yield return null;
        }
        Require(clearStops, "Transit keeps distinct safe stopping tiles");
        Require(players[0].GridPosition == start + direction * 4 && players[0].MovesRemaining == 2,
            "Passing players spends the normal number of moves");
        for (int i = 0; i < players.Length; i++) players[i].SetGridPosition(saved[i]);
        foreach (PlayerCharacter player in players) player.SetGridPosition(player.GridPosition);
        players[0].SetGridPosition(start);
        players[1].SetGridPosition(start + direction * 3);
        players[0].SetMovesFromItem(3);
        Require(players[0].MoveToGridPosition(players[1].GridPosition), "Occupied destination uses the preceding clear tile");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == start + direction * 2 && players[0].MovesRemaining == 1,
            "Player stops before occupied endpoint without spending an unused step");
        Require(!players[0].MoveToGridPosition(players[1].GridPosition), "One move cannot stop on the next player");
        //Keyboard and network direction inputs request only the adjacent tile.
        players[0].SetGridPosition(start);
        players[1].SetGridPosition(start + direction);
        players[0].SetMovesFromItem(2);
        Require(players[0].MoveToGridPosition(start + direction), "Adjacent input passes through the player");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == start + direction * 2 && players[0].MovesRemaining == 0,
            "Adjacent input spends two moves and stops on clear floor");
        //Return to the active player after spending the last move.
        var turns = UnityEngine.Object.FindFirstObjectByType<TurnSystemController>();
        for (int i = 0; i < players.Length && turns.CurrentPlayerIndex != 0; i++)
        {
            players[turns.CurrentPlayerIndex].SetMovesFromItem(1);
            turns.AdvanceTurn();
        }
        Require(turns.CurrentPlayerIndex == 0, "Passage check returns to the active player");
        //Put a temporary Freeze event under the other player to check crossed events.
        for (int i = 0; i < players.Length; i++) players[i].SetGridPosition(saved[i]);
        foreach (PlayerCharacter player in players) player.SetGridPosition(player.GridPosition);
        players[0].SetGridPosition(start);
        players[1].SetGridPosition(start + direction);
        var eventTiles = (Dictionary<Vector2Int, MapEventType>)typeof(MapEventController)
            .GetField("events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(events);
        eventTiles.Add(start + direction, MapEventType.Freeze);
        players[0].SetMovesFromItem(3);
        Require(players[0].MoveToGridPosition(start + direction * 2), "Pass through a player standing on an event");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == start + direction * 2 && !players[0].EventState.BeginTurn(),
            "Freeze still activates when crossing an occupied tile");
        eventTiles.Remove(start + direction);
        //Forced movement also passes through players but never finishes on one.
        var moveExtra = typeof(MapEventController).GetMethod("MoveExtra",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        players[0].SetGridPosition(start);
        players[1].SetGridPosition(start + direction * 2);
        moveExtra.Invoke(events, new object[] { players[0], direction, true, start });
        Require(players[0].GridPosition == start + direction && events.CurrentMessage.Contains("Moved 1 spaces"),
            "Extra movement stops before an occupied final tile");
        players[0].SetGridPosition(start);
        players[1].SetGridPosition(start + direction);
        moveExtra.Invoke(events, new object[] { players[0], direction, true, start });
        Require(players[0].GridPosition == start + direction * 2, "Extra movement passes through another player");
        //Neither crossing nor stopping on an event during forced movement applies its effect.
        foreach (MapEventType type in Enum.GetValues(typeof(MapEventType)))
        {
            players[0].SetGridPosition(start);
            eventTiles.Add(start + direction, type);
            eventTiles.Add(start + direction * 2, type);
            int remaining = players[0].MovesRemaining;
            moveExtra.Invoke(events, new object[] { players[0], direction, true, start });
            Require(players[0].GridPosition == start + direction * 2 && players[0].MovesRemaining == remaining,
                "Forced movement ignores crossed and destination " + type);
            Require(players[0].EventState.BeginTurn() && !players[0].EventState.HasLuckyRoll && events.BlackoutTurns == 0,
                "Forced movement does not apply another event's status");
            eventTiles.Remove(start + direction);
            eventTiles.Remove(start + direction * 2);
        }
        for (int i = 0; i < players.Length; i++) players[i].SetGridPosition(saved[i]);
        foreach (PlayerCharacter player in players) player.SetGridPosition(player.GridPosition);
        Debug.Log("PASS-THROUGH CHECKS PASSED: " + players.Length + " players");
    }

    //Walks onto the real switch tile and checks movement, inventory, and destination events.
    private static IEnumerator CheckLocationSwitch(IsometricMapTemplate map, PlayerCharacter[] players, TurnSystemController turns)
    {
        //Rare events are optional in real matches, so create missing fixtures for this check.
        foreach (MapEventType type in new[] { MapEventType.LocationSwitch, MapEventType.Freeze, MapEventType.LuckyRoll, MapEventType.Blackout })
            EnsureEvent(map, type);
        EventTileMarker[] markers = UnityEngine.Object.FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None);
        Vector2Int switchCell = map.WorldToGrid(Array.Find(markers, marker => marker.EventType == MapEventType.LocationSwitch).transform.position);
        var saved = new Vector2Int[players.Length];
        var oldCells = new Vector2Int[players.Length];
        var held = new Escape4Now.Items.Item[players.Length];
        MapEventType[] otherEvents = { MapEventType.Freeze, MapEventType.LuckyRoll, MapEventType.Blackout };
        Require(!players[0].CanBeTargetedBy(players[0]), "Players cannot target themselves");
        Require(players[1].RecordSuccessfulTargetedAction(players[0]), "Successful targeting grants protection");
        Require(!players[1].CanBeTargetedBy(players[0]) && !players[1].RecordSuccessfulTargetedAction(players[0]),
            "Protected player rejects another direct action");
        for (int i = 0; i < players.Length; i++)
        {
            saved[i] = players[i].GridPosition;
            players[i].SetMovesFromItem(i == 0 ? 6 : 3);
            held[i] = UnityEngine.Object.Instantiate(map.GetComponent<Escape4Now.Items.ItemSpawner>().GetNetworkPrefab(1));
            held[i].PickUp(players[i].Inventory);
            if (i > 0)
            {
                Vector2Int cell = map.WorldToGrid(Array.Find(markers, marker => marker.EventType == otherEvents[i - 1]).transform.position);
                Require(players[i].SetGridPosition(cell), "Test player stands on a destination event");
            }
        }
        bool approached = false;
        foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            Vector2Int cell = switchCell + step;
            if (!map.IsWalkable(cell) || map.IsOccupied(cell) || map.IsExit(cell)) continue;
            approached = players[0].SetGridPosition(cell);
            if (approached) break;
        }
        Require(approached, "Switch has a reachable approach");
        for (int i = 0; i < players.Length; i++) oldCells[i] = i == 0 ? switchCell : players[i].GridPosition;
        Require(players[0].MoveToGridPosition(switchCell), "Active player walks onto Location Switch");
        while (players[0].IsMoving) yield return null;
        var newCells = new HashSet<Vector2Int>();
        for (int i = 0; i < players.Length; i++)
        {
            Require(players[i].GridPosition != oldCells[i] && Array.IndexOf(oldCells, players[i].GridPosition) >= 0
                && newCells.Add(players[i].GridPosition), "Every player receives someone else's previous tile");
            Require(map.IsOccupied(players[i].GridPosition), "Swapped positions remain occupied on the map");
            Require(players[i].MovesRemaining == (i == 0 ? 5 : 3), "Swap preserves unused movement");
            Require(players[i].Inventory.HasItem(held[i]), "Items stay with their player");
            if (i == 1) Require(players[i].EventState.IsProtectedFromPlayers, "Global Location Switch ignores direct-action protection");
            Require(players[i].EventState.BeginTurn() && !players[i].EventState.HasLuckyRoll,
                "Destination Freeze and Lucky Roll do not trigger");
            Require(!players[i].EventState.IsProtectedFromPlayers, "Protection clears when the next turn begins");
        }
        Require(map.GetComponent<MapEventController>().BlackoutTurns == 0 && turns.CurrentPlayerIndex == 0,
            "Destination Blackout does not trigger and the active turn continues");
        for (int i = 0; i < players.Length; i++)
        {
            players[i].Inventory.RemoveItem(held[i]);
            UnityEngine.Object.Destroy(held[i].gameObject);
            Require(players[i].SetGridPosition(saved[i]), "Restore position after swap test");
        }
        foreach (PlayerCharacter player in players) player.SetGridPosition(player.GridPosition);
        Debug.Log("LOCATION SWITCH CHECKS PASSED: " + players.Length + " players");
    }

    //Adds an optional event only to the test scene when it did not spawn naturally.
    private static void EnsureEvent(IsometricMapTemplate map, MapEventType type)
    {
        foreach (EventTileMarker marker in UnityEngine.Object.FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None))
            if (marker.EventType == type) return;
        var controller = map.GetComponent<MapEventController>();
        var tiles = (Dictionary<Vector2Int, MapEventType>)typeof(MapEventController)
            .GetField("events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
        foreach (Vector2Int cell in map.GetAvailableFloorTiles())
        {
            if (map.IsOccupied(cell) || map.IsExit(cell) || tiles.ContainsKey(cell) || map.IsSpecialItemTile(cell)) continue;
            bool itemHere = false;
            foreach (var item in UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None))
                if (item.IsAtPosition(cell)) itemHere = true;
            if (itemHere) continue;
            var marker = UnityEngine.Object.Instantiate(controller.GetNetworkPrefab((int)type), map.GridToWorld(cell), Quaternion.identity, map.transform);
            marker.Configure(map.TileWidth, map.TileHeight);
            tiles.Add(cell, type);
            return;
        }
        throw new Exception("No tile for event test fixture.");
    }

    //Checks saved effects, immediate moves, safe teleports, and scaled global duration.
    private static void CheckNewEvents(IsometricMapTemplate map, PlayerCharacter[] players)
    {
        var state = new PlayerEventState();
        state.GiveBadRoll(); state.GiveBadRoll(); state.BeginTurn();
        Require(state.UseRoll(2, 5) == 2 && state.UseRoll(5, 2) == 5, "Bad Roll takes the lower die only once");
        state.GiveBadRoll(); state.GiveLuckyRoll(); state.BeginTurn();
        Require(state.UseRoll(2, 5) == 5, "Lucky replaces Bad Roll");
        state.GiveLuckyRoll(); state.GiveBadRoll(); state.BeginTurn();
        state.GiveRollBonus(); state.GiveRollBonus();
        Require(state.UseRoll(2, 5) == 3 && state.UseRoll(2, 5) == 2, "Bonus is one-use and does not stack");
        state.GiveFreeze(); state.GiveFreeze();
        Require(!state.BeginTurn() && state.BeginTurn(), "Freeze skips only one turn");
        state.GiveSecondChance(); state.GiveSecondChance();
        Require(state.BlockNegativeEvent() && !state.BlockNegativeEvent(), "Second Chance blocks only once");

        var controller = map.GetComponent<MapEventController>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var tiles = (Dictionary<Vector2Int, MapEventType>)typeof(MapEventController).GetField("events", flags).GetValue(controller);
        var resolve = typeof(MapEventController).GetMethod("ResolveEvent", flags);
        var resolving = typeof(PlayerCharacter).GetField("resolvingStep", flags);
        Vector2Int origin = players[0].GridPosition;
        bool hadEvent = tiles.TryGetValue(origin, out MapEventType original);
        Action<MapEventType> activate = type =>
        {
            tiles[origin] = type;
            resolving.SetValue(players[0], true);
            try { resolve.Invoke(controller, new object[] { players[0], Vector2Int.right, origin }); }
            finally { resolving.SetValue(players[0], false); }
        };
        try
        {
            players[0].SetMovesFromItem(1);
            activate(MapEventType.ExtraStep);
            Require(players[0].MovesRemaining == 3, "Extra Step adds two current moves");
            activate(MapEventType.Stumble); activate(MapEventType.Stumble);
            Require(players[0].MovesRemaining == 0, "Stumble stops at zero");
            activate(MapEventType.SecondChance); activate(MapEventType.Freeze);
            Require(players[0].EventState.BeginTurn() && !players[0].EventState.HasSecondChance, "Second Chance cancels Freeze");
            activate(MapEventType.SecondChance); activate(MapEventType.Blackout);
            int duration = 6 - players.Length;
            Require(controller.BlackoutTurns == duration && players[0].EventState.HasSecondChance, "Blackout scales and ignores Second Chance");
            var count = typeof(MapEventController).GetMethod("CountBlackoutTurn", flags);
            count.Invoke(controller, null);
            Require(controller.BlackoutTurns == duration, "Blackout excludes its partial starting turn");
            count.Invoke(controller, null);
            activate(MapEventType.Blackout);
            Require(controller.BlackoutTurns == duration - 1, "Repeated Blackout cannot extend darkness");
            for (int i = 1; i < duration; i++) count.Invoke(controller, null);
            Require(controller.BlackoutTurns == 0, "Blackout expires after the required turns");
            activate(MapEventType.GoodLuckDay);
            foreach (PlayerCharacter player in players)
                Require(player.EventState.UseRoll(4, 4) == 5 && player.EventState.UseRoll(4, 4) == 4, "Every player receives one bonus");
            activate(MapEventType.WrongRoom);
            Require(map.GetRoomType(players[0].GridPosition) != map.GetRoomType(origin)
                && map.GetRoomType(players[0].GridPosition) != SchoolRoomType.None
                && map.IsWalkable(players[0].GridPosition) && !controller.HasEvent(players[0].GridPosition), "Wrong Room finds a safe different room");
            activate(MapEventType.FireDrill);
            var positions = new HashSet<Vector2Int>();
            foreach (PlayerCharacter player in players)
                Require(map.IsHallway(player.GridPosition) && map.IsWalkable(player.GridPosition)
                    && !controller.HasEvent(player.GridPosition) && positions.Add(player.GridPosition), "Fire Drill uses distinct safe hallway tiles");
            Require(players[0].EventState.HasSecondChance, "Global and neutral events preserve Second Chance");
            players[0].EventState.BlockNegativeEvent();
        }
        finally { if (hadEvent) tiles[origin] = original; else tiles.Remove(origin); }
        Require(MapEventController.SpawnChance(MapEventType.Freeze) < MapEventController.SpawnChance(MapEventType.ExtraStep),
            "Strong events appear less often");
        Debug.Log("ALL 13 EVENT CHECKS PASSED: " + players.Length + " players");
    }

    //Saves the real game camera at two desktop sizes when a graphics device is available.
    private static void CaptureMap(IsometricMapTemplate map, int width, int height, string label)
    {
        Camera camera = Camera.main;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        try
        {
            target.Create();
            camera.targetTexture = target;
            map.SendMessage("FitCamera");
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            var colors = new HashSet<Color32>();
            Color32[] pixels = image.GetPixels32();
            for (int i = 0; i < pixels.Length; i += 37) colors.Add(pixels[i]);
            Require(colors.Count > 10, "Rendered map is not blank");
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllBytes("Logs/school-map-" + label + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(image);
            map.SendMessage("FitCamera");
        }
    }

    //Names each check in the Unity log.
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Debug.Log("PASS: " + message);
    }

    //Releases test input and closes only the separate test editor.
    private static void Finish(int result)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        EditorApplication.Exit(result);
    }
}
