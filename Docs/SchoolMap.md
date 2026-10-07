# School Map

The map is 18 by 18 tiles. Each game has a Library, Computer Lab, Classroom, and Science Lab.
The rooms change size, shape, and position. Hallways connect them, and every floor tile can be reached.
Room names and floor colors show which area is which.
Rooms fill most of the space, with corner cuts no larger than one tile.
Open doors mark room entrances. They are only visual and do not block movement or have opening rules.
One green EXIT door appears on an outside wall beside clear floor, away from corners.
The door replaces that wall block and sits at floor level.
Reaching the EXIT door uses the existing win code. The match pauses and shows which player escaped.
No keys are needed yet. The doors between rooms are still only visual.

## How It Works

1. Fill the grid with walls.
2. Fill the four areas with rooms about 7 by 7 to 8 by 8 tiles each, with small corner cuts for variety.
3. Connect them with hallways and openings in the walls.
4. Draw thin room walls and keep unused areas flat and dark.
5. Check that all floor tiles connect.

A new match creates a new layout. `LayoutSeed` is the number used to repeat a layout for testing.

## Placing Things Later

Other scripts can use these methods on `IsometricMapTemplate`:

- `GetTileType(cell)` checks if a tile is a wall or floor.
- `GetRoomType(cell)` gets the room theme.
- `IsWalkable(cell)` checks if a player can walk there.
- `TryGetRandomFloor(out cell, reserved)` picks an empty floor tile.
- `GetAvailableFloorTiles(reserved)` lists the empty floor tiles.
- `GridToWorld(cell)` gets a tile's position in the game.
- `TryFindPath(start, end, out path)` finds a walking path.

Keep chosen tiles in a shared `reserved` set so other objects do not use the same spot.
Check that any new obstacles do not block a hallway.

The existing item, event, and obstacle spawners are active in `SampleScene`.
Their scripts and prefabs are unchanged. The obstacle spawner adds the obstacles;
the map does not add extra pillars. The EXIT door uses the existing win logic.

## Test

The layout test checks 10,002 seeds. Run it from the project folder with the .NET 10 SDK:

```powershell
dotnet run --project Tests/SchoolMapChecks/SchoolMapChecks.proj
```

Also check the map in Unity. Start a new match to get a new layout.
