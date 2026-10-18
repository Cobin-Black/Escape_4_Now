using System;
using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Obstacles;
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
        ConfigureItemPlacementChecks(arguments);
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
        Require(!LocalMultiplayerSession.MatchHasEnded, "An unstarted timer does not show a loss screen");
        Require(!session.TryStartLocalMatch(4), "A running match cannot duplicate players");
        for (int i = 0; i < 10; i++) yield return null;
        PlayerCharacter[] players = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        Array.Sort(players, (left, right) => string.CompareOrdinal(left.name, right.name));
        TurnSystemController turns = UnityEngine.Object.FindFirstObjectByType<TurnSystemController>();
        CheckTimeoutResult();
        IsometricMapTemplate map = UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>();
        IEnumerator orderChecks = CheckRandomTurnOrder(map, players, turns);
        while (orderChecks.MoveNext()) yield return orderChecks.Current;
        CheckObstacleCatalog(map);
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
        CheckEventCatalog(events);
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
        CheckUniqueItems(map);
        var lockers = UnityEngine.Object.FindObjectsByType<Escape4Now.Obstacles.ObstacleGridControl>(FindObjectsSortMode.None);
        Require(lockers.Length <= 3, "Only a few lockers use safe hallway positions");
        foreach (var locker in lockers)
        {
            Require(map.IsLockerPosition(locker.GridPosition) && locker.GetComponent<Escape4Now.Obstacles.ObstacleStorage>() != null,
                "Lockers stay beside hallway walls and support storage");
            ObstacleOrientation.GetStep(locker.Facing, out int dx, out int dy);
            Require(map.GetTileType(locker.GridPosition - new Vector2Int(dx, dy)) == SchoolTileType.Wall
                && map.IsWalkable(locker.GridPosition + new Vector2Int(dx, dy)),
                "Locker backs face a wall and doors face open hallway floor");
            bool visibleFront = locker.Facing == ObstacleFacing.South || locker.Facing == ObstacleFacing.West;
            Require((locker.GetComponent<MeshFilter>().sharedMesh.vertexCount > 12) == visibleFront,
                "Locker details appear on the front, never the back");
        }
        foreach (Transform child in map.GetComponentsInChildren<Transform>())
            Require(child.name != "Room Label" && child.name != "School Space Label", "Main and small room names are hidden");
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
        CheckObstacleDrawing(map, templates);
        Require(furniture.Length >= 10 && furniture.Length <= 28, "Required and optional obstacles stay sparse");
        foreach (SchoolSecondarySpace space in map.SecondarySpaces)
        {
            SchoolFurnitureKind[] pool = SchoolFurniturePlan.SpecialPool(space.Type);
            int placed = 0;
            bool hasSignature = false;
            foreach (SchoolFurniture item in furniture)
                if (map.GetSpaceType(item.Cell) == space.Type)
                {
                    placed++;
                    Require(Array.IndexOf(pool, item.Kind) >= 0, "Small-room obstacle matches its theme");
                    hasSignature |= item.Kind == pool[0];
                }
            Require(placed >= 1 && placed <= 2, "Small room draws one or two matching obstacles");
            Require(space.Type == SchoolSpaceType.Storage || hasSignature, "An obstacle identifies the small room");
        }
        foreach (SchoolSecondarySpace room in map.SecondarySpaces)
        {
            int storedRewards = 0;
            foreach (ObstacleStorage storage in map.GetComponentsInChildren<ObstacleStorage>())
                if (storage.HasItem && map.GetSpaceType(storage.GridPosition) == room.Type) storedRewards++;
            Require(storedRewards == 1, "Special room item is stored in one of its obstacles");
            foreach (Escape4Now.Items.Item item in UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None))
                Require(map.GetSpaceType(map.WorldToGrid(item.transform.position)) != room.Type,
                    "Special-room items do not spawn on the floor");
        }
        var furnitureCells = new HashSet<Vector2Int>();
        var expectedFurniture = new SchoolFurniturePlan(new SchoolMapLayout(map.LayoutSeed));
        foreach (SchoolFurniture item in furniture)
        {
            Require(item.transform.Find("Obstacle preview") == null, "Play Mode does not keep editor preview parts");
            Require(furnitureCells.Add(item.Cell) && !cells.Contains(item.Cell) && map.IsBlocked(item.Cell),
                "Furniture reserves separate blocked tiles");
            Require(item.GetComponentsInChildren<MeshRenderer>().Length > 0, "Furniture prefab has visible parts");
            bool matchingDirection = false;
            foreach (SchoolFurniturePlacement expected in expectedFurniture.Furniture)
                if (expected.Cell == item.Cell.y * map.Width + item.Cell.x)
                    matchingDirection = expected.Kind == item.Kind && expected.Facing == item.Facing;
            Require(matchingDirection, "Drawn obstacle uses the direction from its seeded plan");
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
            yield return null;
            players[i].SetMovesFromItem(1);
            turns.AdvanceTurn();
        }
        Require(turns.CurrentPlayerIndex == 0 && turns.TurnNumber == 2, "Round returns to Player 1");
        IEnumerator movementChecks = CheckOccupiedMovement(map, players);
        while (movementChecks.MoveNext()) yield return movementChecks.Current;
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
        Require(!players[0].MoveToGridPosition(exitCell) && !players[0].IsMoving,
            "The exit stays locked without a key and does not leave movement stuck");
        IEnumerator keyChecks = CheckKeys(map, players, turns);
        while (keyChecks.MoveNext()) yield return keyChecks.Current;
        Require(players[0].SetGridPosition(approach), "Key holder can approach the exit");
        players[0].SetMovesFromItem(1);
        Require(players[0].MoveToGridPosition(exitCell), "Player can walk through the exit");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == exitCell && Time.timeScale == 0f, "Reaching the door pauses the match for the win");
        Require(LocalMultiplayerSession.MatchHasEnded, "Escaping selects the match result screen");
        typeof(PlayerCharacter).GetMethod("ReachExit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(players[1], null);
        Require(!players[1].HasReachedExit, "Only the first player can escape");
        Time.timeScale = 1f;
        Debug.Log("SAME-DEVICE AND SCHOOL MAP CHECKS PASSED");
    }

    private static void CheckEventCatalog(MapEventController controller)
    {
        int typeCount = Enum.GetValues(typeof(MapEventType)).Length;
        var configuration = new SerializedObject(controller);
        Require(configuration.FindProperty("eventPrefabs").arraySize == typeCount,
            "The event Inspector includes every event prefab");
        var templates = new EventTileMarker[typeCount];
        for (int type = 0; type < typeCount; type++)
        {
            templates[type] = controller.GetNetworkPrefab(type);
            Require(templates[type] != null && (int)templates[type].EventType == type,
                "Event prefab lookup preserves its type: " + (MapEventType)type);
        }

        var testObject = new GameObject("Event catalog check");
        testObject.SetActive(false);
        try
        {
            var migratedController = testObject.AddComponent<MapEventController>();
            var migratedConfiguration = new SerializedObject(migratedController);
            var catalog = migratedConfiguration.FindProperty("eventPrefabs");
            catalog.arraySize = 5;
            for (int type = 0; type < 5; type++) catalog.GetArrayElementAtIndex(type).objectReferenceValue = templates[type];
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            Require(migratedController.GetNetworkPrefab(typeCount - 1) != null,
                "The old five-event configuration migrates to the complete event list");
            migratedConfiguration.Update();
            Require(catalog.arraySize == typeCount, "Migrated event references are exposed in the Inspector");

            var replacement = UnityEngine.Object.Instantiate(templates[0], testObject.transform);
            catalog.GetArrayElementAtIndex(0).objectReferenceValue = replacement;
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(MapEventController).GetMethod("OnValidate", flags).Invoke(migratedController, null);
            Require(migratedController.GetNetworkPrefab(0) == replacement, "An Inspector-assigned event prefab overrides the default");

            catalog.arraySize = 0;
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            typeof(MapEventController).GetMethod("OnValidate", flags).Invoke(migratedController, null);
            Require(migratedController.GetNetworkPrefab(0) == null && migratedController.GetNetworkPrefab(typeCount - 1) == null,
                "Removing events does not silently restore them from Resources");
            catalog.arraySize = 1;
            catalog.GetArrayElementAtIndex(0).objectReferenceValue = replacement;
            var chances = migratedConfiguration.FindProperty("spawnChances");
            chances.arraySize = 1;
            chances.GetArrayElementAtIndex(0).FindPropertyRelative("eventType").intValue = 0;
            chances.GetArrayElementAtIndex(0).FindPropertyRelative("chance").floatValue = 0f;
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            var getChance = typeof(MapEventController).GetMethod("GetSpawnChance", flags);
            Require((float)getChance.Invoke(migratedController, new object[] { MapEventType.Warp }) == 0f,
                "Inspector chance zero disables an included event");
            chances.GetArrayElementAtIndex(0).FindPropertyRelative("chance").floatValue = 1f;
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            Require((float)getChance.Invoke(migratedController, new object[] { MapEventType.Warp }) == 1f,
                "Inspector chance one guarantees an included event");
        }
        finally { UnityEngine.Object.Destroy(testObject); }
    }

    private static void CheckObstacleCatalog(IsometricMapTemplate map)
    {
        var spawner = map.GetComponent<ObstacleSpawner>();
        var furniture = new List<SchoolFurniture>(spawner.GetSchoolPrefabs());
        var kinds = new HashSet<SchoolFurnitureKind>();
        foreach (SchoolFurniture prefab in furniture)
        {
            Require(prefab.IsValidTemplate(), "Obstacle catalog has a valid furniture prefab: " + prefab.name);
            kinds.Add(prefab.Kind);
        }
        Require(kinds.Count == Enum.GetValues(typeof(SchoolFurnitureKind)).Length,
            "The spawner sections contain every school furniture kind");
        foreach (SchoolRoomType room in Enum.GetValues(typeof(SchoolRoomType)))
        {
            var roomKinds = new HashSet<SchoolFurnitureKind>();
            foreach (SchoolFurniture prefab in spawner.GetRoomPrefabs(room)) roomKinds.Add(prefab.Kind);
            foreach (SchoolFurnitureKind kind in SchoolFurniturePlan.Pool(room))
                Require(roomKinds.Contains(kind), $"The {room} section has its {kind} prefab");
        }
        Require(spawner.GetNetworkPrefab() != null, "The hallway section contains the hallway locker prefab");

        var migrationObject = new GameObject("Obstacle catalog check");
        migrationObject.SetActive(false);
        try
        {
            var migratedSpawner = migrationObject.AddComponent<ObstacleSpawner>();
            var migratedConfiguration = new SerializedObject(migratedSpawner);
            var legacyCatalog = migratedConfiguration.FindProperty("obstaclePrefabs");
            legacyCatalog.arraySize = 1;
            legacyCatalog.GetArrayElementAtIndex(0).objectReferenceValue = spawner.GetNetworkPrefab();
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            Require(new List<SchoolFurniture>(migratedSpawner.GetSchoolPrefabs()).Count == furniture.Count,
                "A saved locker-only configuration migrates to the complete room sections");
            Require(migratedSpawner.GetNetworkPrefab() == spawner.GetNetworkPrefab(),
                "Migration moves the existing locker prefab into the hallway section");
            migratedConfiguration.Update();
            var library = migratedConfiguration.FindProperty("libraryPrefabs");
            library.arraySize = 0;
            migratedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            Require(new List<SchoolFurniture>(migratedSpawner.GetRoomPrefabs(SchoolRoomType.Library)).Count == 0,
                "Later Inspector edits are preserved instead of silently refilling a section");
        }
        finally { UnityEngine.Object.Destroy(migrationObject); }
    }

    private static void CheckTimeoutResult()
    {
        GameTimer timer = UnityEngine.Object.FindFirstObjectByType<GameTimer>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var remaining = typeof(GameTimer).GetField("timeRemaining", flags);
        var running = typeof(GameTimer).GetField("timerRunning", flags);
        float previousRemaining = timer.TimeRemaining;
        object previousRunning = running.GetValue(timer);
        float previousScale = Time.timeScale;
        try
        {
            remaining.SetValue(timer, 0.000001f);
            typeof(GameTimer).GetMethod("Update", flags).Invoke(timer, null);
            Require(timer.HasExpired && timer.TimeRemaining == 0f, "Timer reports actual expiry");
            Require(LocalMultiplayerSession.MatchHasEnded && Time.timeScale == 0f,
                "Timeout selects the result screen and stops gameplay");
            PlayerCharacter player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            typeof(PlayerCharacter).GetMethod("ReachExit", flags).Invoke(player, null);
            Require(!player.HasReachedExit, "Nobody can escape after time expires");
        }
        finally
        {
            remaining.SetValue(timer, previousRemaining);
            running.SetValue(timer, previousRunning);
            Time.timeScale = previousScale;
        }
        Require(!LocalMultiplayerSession.MatchHasEnded, "An unfinished match keeps the gameplay screen");
    }

    private static IEnumerator CheckRandomTurnOrder(IsometricMapTemplate map, PlayerCharacter[] players,
        TurnSystemController turns)
    {
        var names = Array.ConvertAll(players, player => player.name);
        var inventories = Array.ConvertAll(players, player => player.Inventory);
        var positions = Array.ConvertAll(players, player => player.GridPosition);
        var colors = Array.ConvertAll(players, player => new SerializedObject(player).FindProperty("playerColor").colorValue);
        int[] order = turns.CopyTurnOrder();
        Require(new HashSet<int>(order).Count == players.Length, "Shuffled sequence contains every identity once");
        UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
        turns.SetPlayers(players, map.LayoutSeed);
        turns.RestartTurnCycle();
        Require(string.Join(",", order) == string.Join(",", turns.CopyTurnOrder()), "Map seed reproduces the starting sequence");
        Require(JsonUtility.ToJson(randomBefore) == JsonUtility.ToJson(UnityEngine.Random.state),
            "Turn shuffling does not consume dice or event randomness");
        for (int round = 1; round <= 3; round++)
        for (int position = 0; position < order.Length; position++)
        {
            int slot = order[position];
            Require(turns.CurrentPlayerIndex == slot && turns.TurnNumber == round,
                "The same shuffled sequence repeats with the correct round counter");
            Require(turns.CurrentTurnName == names[slot] && turns.IsPlayersTurn(players[slot]),
                "HUD identity and turn ownership use the active participant, not their turn position");
            for (int i = 0; i < players.Length; i++)
                Require(turns.IsPlayersTurn(players[i]) == (i == slot), "Exactly one player owns the turn");
            yield return null;
            players[slot].SetMovesFromItem(1);
            turns.AdvanceTurn();
        }
        Require(turns.CurrentPlayerIndex == order[0] && turns.TurnNumber == 4,
            "Three full rounds return to the original shuffled first player");
        Time.timeScale = 0f;
        turns.AdvanceTurn();
        Require(turns.CurrentPlayerIndex == order[0] && turns.TurnNumber == 4, "Pause blocks shuffled turn advancement");
        Time.timeScale = 1f;
        players[order[0]].EventState.GiveFreeze();
        turns.RestartTurnCycle();
        Require(turns.CurrentPlayerIndex == order[1] && turns.TurnNumber == 1,
            "Freeze skips to the next chosen player without reshuffling");
        Require(string.Join(",", order) == string.Join(",", turns.CopyTurnOrder()), "Restarts and skips preserve match order");

        LanSnapshot snapshot = new LanWorldView(UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>()).Capture();
        snapshot = JsonUtility.FromJson<LanSnapshot>(JsonUtility.ToJson(snapshot));
        Require(LanWorldView.IsValid(snapshot), "Shuffled match produces a valid host snapshot");
        var replicaObject = new GameObject("Turn order test replica");
        var replica = replicaObject.AddComponent<TurnSystemController>();
        replica.enabled = false;
        replica.SetPlayers(players, int.MinValue);
        Require(replica.ApplyNetworkTurn(snapshot.turn, snapshot.round, snapshot.turnOrder)
            && replica.CurrentPlayerIndex == order[1] && replica.CurrentTurnName == names[order[1]],
            "Clients follow host identity slots and order even with a different local seed");
        int[] changedOrder = (int[])order.Clone();
        int saved = changedOrder[0]; changedOrder[0] = changedOrder[1]; changedOrder[1] = saved;
        Require(!replica.ApplyNetworkTurn(snapshot.turn, snapshot.round, changedOrder),
            "Clients reject an order change during the match");
        snapshot.turnOrder = new int[players.Length];
        Require(!LanWorldView.IsValid(snapshot), "Network rejects repeated identities in the sequence");
        UnityEngine.Object.Destroy(replicaObject);
        for (int i = 0; i < players.Length; i++)
        {
            Require(players[i].name == names[i] && players[i].Inventory == inventories[i]
                && players[i].GridPosition == positions[i]
                && new SerializedObject(players[i]).FindProperty("playerColor").colorValue == colors[i],
                "Turn order never changes names, colors, inventories, or map positions");
            players[i].BeginEventTurn();
        }
        //The existing gameplay scenarios use identity-ordered fixtures, not a first-player assumption in the game.
        var settings = new SerializedObject(turns);
        settings.FindProperty("randomizeStartingOrder").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        turns.SetPlayers(players, map.LayoutSeed);
        turns.RestartTurnCycle();
        yield return null;
    }

    //Checks the new key rules together with storage, protection, and shared player tiles.
    private static IEnumerator CheckKeys(IsometricMapTemplate map, PlayerCharacter[] players, TurnSystemController turns)
    {
        var spawner = map.GetComponent<Escape4Now.Items.ItemSpawner>();
        ObstacleStorage locker = Array.Find(map.GetComponentsInChildren<ObstacleStorage>(),
            storage => storage.StoredItemPrefab is Escape4Now.Items.KeyItem);
        Require(locker != null, "One searchable obstacle holds the guaranteed key");
        bool approached = false;
        foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            Vector2Int cell = locker.GridPosition + direction;
            if (map.IsWalkable(cell) && !map.IsExit(cell)) { approached = players[0].SetGridPosition(cell); break; }
        }
        Require(approached, "The key locker has an accessible side");
        var held = UnityEngine.Object.Instantiate(spawner.GetNetworkPrefab(1));
        held.PickUp(players[0].Inventory);
        Require(players[0].Inventory.IsFull && locker.TakeNetworkItem(players[0]) && players[0].Inventory.HasKey(),
            "A key uses its own slot even when the regular inventory is full");
        Require(!locker.HasItem && !locker.TakeNetworkItem(players[0]), "The key can only be collected once");
        var key = players[0].Inventory.KeyItems[0];
        Require(!key.gameObject.activeSelf, "Collected keys do not appear as off-map network pickups");
        players[0].Inventory.RemoveItem(held);
        UnityEngine.Object.Destroy(held.gameObject);
        players[0].Inventory.RemoveKey();
        players[1].Inventory.GiveKey(key);
        players[1].SetGridPosition(players[0].GridPosition);
        players[1].EventState.BeginTurn();
        var battle = UnityEngine.Object.FindFirstObjectByType<Escape4Now.GameModes.Race_Gamemode>();
        Require(battle != null && battle.isActiveAndEnabled, "The merged scene contains the key battle controller");
        UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool win = attempt == 0;
            int seed;
            for (seed = 0; seed < 10000; seed++)
            {
                UnityEngine.Random.InitState(seed);
                int first = UnityEngine.Random.Range(1, 7), second = UnityEngine.Random.Range(1, 7);
                if (first != second && (first > second) == win) break;
            }
            Require(seed < 10000, "A repeatable battle test roll exists");
            UnityEngine.Random.InitState(seed);
            Require(battle.TryNetworkBattle(players[0], players[1]), "Players sharing a tile may battle for the key");
            Require(players[0].IsInMenu() && !string.IsNullOrEmpty(battle.NetworkMessage), "Battle result pauses the challenger");
            Require(!battle.FinishNetworkBattle(players[1]), "Another player cannot close the battle");
            Require(battle.FinishNetworkBattle(players[0]), "The challenger can close the result");
            if (win)
            {
                Require(players[0].Inventory.HasKey() && !players[1].Inventory.HasKey()
                    && players[1].EventState.IsProtectedFromPlayers && turns.CurrentPlayerIndex == 0,
                    "A win transfers the key, protects the victim, and keeps the turn");
                players[0].Inventory.RemoveKey();
                players[1].Inventory.GiveKey(key);
                Require(!battle.TryNetworkBattle(players[0], players[1]), "Repeated targeting is rejected");
                players[1].EventState.BeginTurn();
            }
            else Require(turns.CurrentPlayerIndex == 1 && players[1].Inventory.HasKey(), "A battle loss ends the challenger's turn");
        }
        UnityEngine.Random.state = savedRandom;
        for (int i = 1; i < players.Length; i++) { players[i].SetMovesFromItem(1); turns.AdvanceTurn(); }
        yield return null;
        Require(players[1].RecordSuccessfulTargetedAction(players[0]), "The steal test starts with a protected victim");
        var stealer = UnityEngine.Object.Instantiate(spawner.GetNetworkPrefab(3));
        stealer.PickUp(players[0].Inventory);
        stealer.Use(players[0].Inventory);
        Require(players[1].Inventory.HasKey() && players[0].Inventory.HasItem(stealer), "Key Stealer cannot bypass target protection");
        players[1].EventState.BeginTurn();
        stealer.Use(players[0].Inventory);
        Require(players[0].Inventory.HasKey() && !players[1].Inventory.HasKey()
            && !players[0].Inventory.HasItem(stealer) && players[1].EventState.IsProtectedFromPlayers,
            "Key Stealer transfers the key once and protects the victim");
        Require(LanWorldView.IsValid(new LanWorldView(UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>()).Capture()),
            "Collected keys and shared tiles produce a valid network snapshot");
        UnityEngine.Object.Destroy(stealer.gameObject);
        Debug.Log("KEY AND SHARED-TILE BATTLE CHECKS PASSED");
    }

    //Test both floor-only and container placement without changing the saved scene.
    private static void ConfigureItemPlacementChecks(string[] arguments)
    {
        var spawner = UnityEngine.Object.FindFirstObjectByType<ObstacleSpawner>();
        var settings = new SerializedObject(spawner);
        if (Array.IndexOf(arguments, "-emptyStorage") >= 0) settings.FindProperty("interactableChance").floatValue = 0f;
        if (Array.IndexOf(arguments, "-fullStorage") >= 0) settings.FindProperty("interactableChance").floatValue = 1f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    //Items now come only from obstacles, using the current gamemode's list. The Race key is placed once by the gamemode.
    private static void CheckUniqueItems(IsometricMapTemplate map)
    {
        var spawner = map.GetComponent<Escape4Now.Items.ItemSpawner>();
        var obstacles = map.GetComponent<ObstacleSpawner>();
        Require(spawner.HasReservedSpecialRoomItems && spawner.HasSpawnedItems && obstacles.HasSpawnedObstacles,
            "Item and obstacle placement finishes without waiting on each other");
        var allowed = new HashSet<Type>();
        foreach (var option in spawner.GetItemOptions(spawner.CurrentGameMode))
            if (option != null && option.Prefab != null && option.SpawnChance > 0f) allowed.Add(option.Prefab.GetType());
        Require(!allowed.Contains(typeof(Escape4Now.Items.KeyItem)), "The key is not part of any item list");
        var containers = map.GetComponentsInChildren<ObstacleStorage>();
        int keys = 0, stored = 0;
        var specialStorageCounts = new Dictionary<SchoolSpaceType, int>();
        foreach (ObstacleStorage storage in containers)
            if (storage.HasItem)
            {
                stored++;
                SchoolSpaceType roomType = map.GetSpaceType(storage.GridPosition);
                if (storage.StoredItemPrefab is Escape4Now.Items.KeyItem)
                {
                    keys++;
                    Require(roomType == SchoolSpaceType.None, "The key stays out of every special room");
                }
                else Require(allowed.Contains(storage.StoredItemPrefab.GetType()), "Stored items come from the gamemode's list");
                if (roomType != SchoolSpaceType.None)
                {
                    specialStorageCounts.TryGetValue(roomType, out int count);
                    specialStorageCounts[roomType] = count + 1;
                }
            }
        bool race = spawner.CurrentGameMode == Escape4Now.GameModes.GameModeType.Race;
        Require(keys == (race ? 1 : 0), "The Race gamemode hides exactly one key; other gamemodes hide none");
        Require(map.SpecialItemCells.Count == map.SecondarySpaces.Count, "Every small room reserves a clear tile beside its furniture");
        foreach (SchoolSecondarySpace room in map.SecondarySpaces)
        {
            specialStorageCounts.TryGetValue(room.Type, out int storedRewards);
            Require(storedRewards == 1, "Each special room has one item inside an obstacle");
        }
        Require(!spawner.TryTakeStorageItem(out _), "Finished placement cannot refill used obstacles");
        foreach (ObstacleStorage storage in containers)
        {
            var saved = storage.StoredItemPrefab;
            obstacles.FillStorage(storage);
            Require(storage.StoredItemPrefab == saved, "Repeating a fill leaves finished contents unchanged");
        }
        string[] arguments = Environment.GetCommandLineArgs();
        int special = map.SecondarySpaces.Count;
        if (Array.IndexOf(arguments, "-emptyStorage") >= 0)
            Require(stored == special + keys, "With no random chance only special rooms and the key hold items");
        Debug.Log("ITEM SPAWN AND SPECIAL ROOM CHECKS PASSED");
    }

    //Checks the shared-tile rule from main without bringing back the old two-step crossing rule.
    private static IEnumerator CheckOccupiedMovement(IsometricMapTemplate map, PlayerCharacter[] players)
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
        players[0].SetGridPosition(start);
        players[1].SetGridPosition(start + direction);
        players[0].SetMovesFromItem(6);
        Require(players[0].MoveToGridPosition(start + direction), "Players may enter a shared tile");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == players[1].GridPosition && players[0].MovesRemaining == 5,
            "Entering another player's tile costs one move, not two");
        Require(Vector3.Distance(players[0].transform.position, players[1].transform.position) > 0.1f,
            "Both players remain visible on a shared tile");
        //Four players can swap even when two begin together. With fewer players it is impossible to move everyone.
        var switchMethod = typeof(MapEventController).GetMethod("SwitchLocations",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var beforeSwitch = new Vector2Int[players.Length];
        for (int i = 0; i < players.Length; i++) beforeSwitch[i] = players[i].GridPosition;
        bool switched = (bool)switchMethod.Invoke(null, new object[] { players });
        Require(switched == (players.Length == 4), "Location Switch handles shared starting positions safely");
        for (int i = 0; i < players.Length; i++)
        {
            Require((players[i].GridPosition != beforeSwitch[i]) == switched, "Location Switch moves everyone or leaves everyone in place");
            players[i].SetGridPosition(beforeSwitch[i]);
        }
        var sharedSnapshot = new LanWorldView(UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>()).Capture();
        Require(LanWorldView.IsValid(sharedSnapshot), "Wi-Fi snapshots allow the new shared-tile rule");
        Require(players[0].MoveToGridPosition(start + direction * 3), "A player can leave a shared tile");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == start + direction * 3 && players[0].MovesRemaining == 3 && players[0].IsCurrentTurn,
            "Movement charges only the completed steps");
        Require(map.IsOccupied(players[1].GridPosition), "Leaving a shared tile keeps the remaining player registered");
        players[0].SetGridPosition(start);
        Require(players[0].MoveToGridPosition(start + direction * 2), "A route can pass another player");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == start + direction * 2 && players[0].MovesRemaining == 1,
            "Passing another player spends one move per tile and does not skip a turn");
        var eventTiles = (Dictionary<Vector2Int, MapEventType>)typeof(MapEventController)
            .GetField("events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(events);
        //Forced movement also stops before another player.
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
        Require(players[0].GridPosition == start, "Extra movement does not cross an adjacent player");
        players[1].SetGridPosition(saved[1]);
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
        Debug.Log("OCCUPIED MOVEMENT CHECKS PASSED: " + players.Length + " players");
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

    //Checks every prefab in all four directions without changing the running map.
    private static void CheckObstacleDrawing(IsometricMapTemplate map, SchoolFurniture[] templates)
    {
        UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
        try
        {
            foreach (SchoolFurniture template in templates)
                for (int direction = 0; direction < 4; direction++)
                {
                    SchoolFurniture copy = UnityEngine.Object.Instantiate(template);
                    copy.transform.position = new Vector3(1000, 1000, 0);
                    try
                    {
                        copy.Build(map, new Vector2Int(1, 1), (ObstacleFacing)direction);
                        Require(copy.transform.rotation == Quaternion.identity
                            && copy.GetComponent<SortingGroup>().sortingOrder == 480,
                            "Rotated obstacle stays upright and on its tile's drawing layer");
                        var meshes = new List<MeshFilter>(copy.GetComponentsInChildren<MeshFilter>());
                        var data = new SerializedObject(template);
                        SerializedProperty parts = data.FindProperty("parts");
                        Require(meshes.Count == parts.arraySize, "Turning keeps every prefab part");
                        for (int i = 0; i < parts.arraySize; i++)
                        {
                            SerializedProperty part = parts.GetArrayElementAtIndex(i);
                            Vector2 offset = part.FindPropertyRelative("offset").vector2Value;
                            float width = part.FindPropertyRelative("width").floatValue;
                            float depth = part.FindPropertyRelative("depth").floatValue;
                            float bottom = part.FindPropertyRelative("bottom").floatValue;
                            float height = part.FindPropertyRelative("height").floatValue;
                            //These expected coordinates are independent of the drawing method.
                            Vector2 turned = direction == 0 ? offset : direction == 1 ? new Vector2(offset.y, -offset.x)
                                : direction == 2 ? -offset : new Vector2(-offset.y, offset.x);
                            if (direction % 2 == 1) { float saved = width; width = depth; depth = saved; }
                            Require(Mathf.Abs(turned.x) + width / 2 <= 0.4501f
                                && Mathf.Abs(turned.y) + depth / 2 <= 0.4501f, "Turned part stays inside its original tile");
                            var center = new Vector3((turned.x - turned.y) * map.TileWidth / 2,
                                (turned.x + turned.y) * map.TileHeight / 2 + bottom + height / 2, 0);
                            var size = new Vector3((width + depth) * map.TileWidth / 2,
                                (width + depth) * map.TileHeight / 2 + height, 0);
                            int match = meshes.FindIndex(mesh => (mesh.sharedMesh.bounds.center - center).sqrMagnitude < 0.000001f
                                && (mesh.sharedMesh.bounds.size - size).sqrMagnitude < 0.000001f);
                            Require(match >= 0, "Drawn part matches the requested grid direction");
                            meshes.RemoveAt(match);
                        }
                    }
                    finally
                    {
                        foreach (MeshFilter mesh in copy.GetComponentsInChildren<MeshFilter>())
                            UnityEngine.Object.DestroyImmediate(mesh.sharedMesh);
                        UnityEngine.Object.DestroyImmediate(copy.gameObject);
                    }
                }
        }
        finally { UnityEngine.Random.state = savedRandom; }
        Debug.Log("OBSTACLE DRAWING CHECKS PASSED: 22 prefabs in all four directions");
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
