# School Map

The map is 25 by 25 tiles. Each game has a Library, Computer Lab, Classroom, and Science Lab.
The rooms change size, position, and shape. Each map includes an L-shaped room, with rectangles or more L shapes for the others.
Hallways connect them, and every floor tile can be reached.
Furniture and floor colors show which area is which, without large room-name labels.
Rooms are placed freely, not in four fixed sections. Unused space stays blocked.
Hallways use lighter gray-green floor tiles. Small secondary spaces use a separate muted floor color and matching obstacles, without labels.
Sealed areas have low solid wall bases instead of looking like missing floor.
Open doors mark room entrances. They are only visual and do not block movement or have opening rules.
One green EXIT door appears on an outside wall beside clear floor, away from corners.
The door replaces that wall block and sits at floor level.
Reaching the EXIT door uses the existing win code. The match pauses and shows which player escaped.
The current scene requires the key to use the exit. The doors between rooms are still only visual.

## How It Works

1. Fill the grid with walls.
2. Place four rooms with sides from 6 to 9 tiles. Grow them into nearby space, up to 12 tiles per side, keeping wall gaps.
3. Cut a corner from some rooms to make L shapes. Keep their centers clear for player spawning.
4. Build shared hallways outside the rooms. Leave wall space beside them and prefer shorter routes with fewer turns.
5. Give every room one entrance. Some get a second, at least four grid steps from any other door. Hallways only touch room floor at these doors.
6. Widen hallway sections and intersections to leave space for obstacles, keeping room walls and door openings clear.
7. When spare space fits, optionally add one small connected school space. Draw walls around the remaining blocked areas.
8. Check all floor tiles with a flood fill, then check coverage and unused space. Retry layouts that fail.

## Secondary Spaces

A map may have no secondary space, or one small 2 by 3, 3 by 2, or 3 by 3 space.
It can be a bathroom, storage room, janitor closet, study area, office, or locker area.
These spaces open onto hallways. The four main room types stay unchanged.
Every small room gets one or two matching obstacles, with entrances and walking paths kept clear.
Every generated small room gets exactly one random item stored inside one of its obstacles, including offices, bathrooms, study areas, locker areas, storage rooms, and janitor closets.
A reachable floor tile is also reserved by the furniture plan; events cannot use that tile.
Storage uses shelves, boxes, cabinets, carts, or crates. Janitor Closets include cleaning equipment.
Bathrooms start with a sink, study areas with a reading table, offices with a desk, and locker areas with lockers.
An optional second object comes from that room's pool. These reuse the existing obstacle prefabs and item storage.
No room has a large floor label.
Positions and types come from the map seed, so all computers draw the same environment.

The Item Spawner has separate weighted prefab lists for Race, Monster, Free-for-All, and Untitled modes.
Small-room rewards are chosen first, then ordinary main-room/hallway containers use the obstacle's item chance and the current mode's item weights.
Ordinary item types may repeat. **Item Spawner > Ordinary Item Budget > Maximum Ordinary Items** limits ordinary filled containers to four by default. The one Race key and the one reward per generated small room are separate from this cap. Each small room still gets exactly one filled container, using the same search system.
Race's default usable items are Double Dice, Custom Dice, and Key Stealer. Its one separate key is assigned on Race_Gamemode and placed in a searchable main-room or hallway container.
Taking or using an item does not refill the container. A new match creates fresh contents.

`GetHallwayDecorationWalls()` returns blocked wall tiles beside hallways,
away from doorways. Future decorations must fit within those tiles and must not cover walking paths.

## Obstacles and Player Starts

The reusable room-object prefabs are in `Assets/Prefabs/Obstacles/Resources/SchoolObstacles`.
The Obstacle Spawner exposes separate Computer Lab, Classroom, Hallway, Library, and Science Lab prefab sections. **Special Room Obstacle Prefabs** adds Bathroom, Storage Room, Janitor Closet, Study Area, Office, and Locker Area lists. Edit each section in Unity to control which prefabs that room can use. Special rooms no longer borrow from unrelated sections. A missing required kind reports the specific room and prefab kind rather than silently substituting another room's catalog.
Furniture counts as an obstacle. Each piece blocks its tile and uses the same one-item storage as lockers.
Their small colored parts are simple placeholders drawn in the same style as the map.
Each prefab fits inside one tile. Its `Parts` fields control its shape and colors.

Each main room has required furniture, then a few optional pieces:

- Library: 2-5 bookshelves, 0-3 reading tables, and 0-1 book cart.
- Computer Lab: 2-7 computer desks and 0-1 printer.
- Classroom: 3-10 student desks, one whiteboard, and 0-1 teacher desk.
- Science Lab: 2-6 lab tables, 0-1 cabinet, and 0-1 sink station.
- Hallways: up to two wall-side decorations and up to three separate lockers, spaced apart.

These are the saved Indoor scene ranges. Counts scale with the actual room floor area, including L-shaped cutouts, and seeded variation. A room does not have to reach its maximum. Optional pieces are skipped when they cannot fit safely; required pieces trigger another arrangement attempt rather than accepting an incomplete room.

## Procedural Furniture Patterns

The existing furniture planner now selects a weighted Rows, Perimeter, Groups, or Center Aisle arrangement for each room. Each room has its own weights; the Classroom disables Perimeter by default. Orientation and row phase also vary with the map seed. Incompatible patterns are excluded before selection. Disabling all compatible patterns produces a clear error, not a random-scatter fallback.

Whiteboards and teacher desks prefer the selected front wall. Printers, cabinets, and sinks prefer room edges. Library perimeter layouts favor wall shelves, while reading tables prefer open central floor and book carts stay near shelves. Workstation and lab-table layouts use aligned rows, small groups, or flanking blocks around an aisle, clipped to the real room shape.

Door-to-center walking routes are reserved before any furniture is placed. Center Aisle patterns additionally reserve the configured aisle stripe. Placement preserves these routes, doorway clearance, a safe player start, a reachable same-area approach to every container, and connectivity of all remaining floor. With **Prevent New Dead Ends** enabled, main-room and hallway furniture cannot create a new one-tile leaf. Small optional rooms retain their compact one- or two-object layout.

On **Race Indoor Map > Furniture Settings**, edit:

- Per-room minimum/maximum counts and classroom whiteboard count.
- **Room Layout Patterns:** weights, row spacing, aisle width, group spacing, wall preference, randomized orientation, or a fixed front wall.
- **Spacing and Safe Access:** doorway, furniture, hallway, spawn, and center clearances, plus dead-end prevention.
- **Room Size and Density:** sparse/full room area thresholds, count variation, and optional furniture chance.
- **Placement Limits:** arrangement retries and candidate checks per object.

Hallway widening, enclosed wall-island cleanup, second-entrance chance, and route length/bend weights are under **School Layout Rules**. Cleanup only removes tiny isolated wall components surrounded by hallways; room walls, doors, and the outer boundary are protected. Existing floor-coverage validation still applies afterward.

**Wall Geometry**, **Door Appearance**, and **Rendering and Camera** expose wall heights/thickness, door colors, the shared material, checker contrast, camera reference/offset, and framing padding. The scene reuses the existing material and room color themes. No new obstacle types, controls, or pause actions were introduced.

After furniture positions are final, the map shuffles the four rooms and picks a clear start in each.
Two or three players use the first two or three rooms in that shuffled order.
Starts stay at least three steps from doorways and leave a clear tile between players and furniture.
Each object blocks one tile. A flood fill checks that every remaining floor tile is still reachable.
An object is skipped when it cannot fit safely.

The map seed controls both furniture and starts. A new random map changes the choices;
a fixed seed repeats them for testing and for players connected over Wi-Fi.
The existing item, event, and obstacle systems start afterward and see furniture as blocked floor.
Furniture uses the existing one-item storage. Stand beside an object and use Interact
to search it. Full inventories leave normal items in place; the key has a separate slot. During network play, the host
checks the player's turn and distance before giving the item.

## Space Usage

The outside border is not counted. The 23 by 23 interior has 529 tiles.
Each accepted map has 344 to 396 walkable tiles, roughly 65 to 75 percent of that interior.
Room floors, hallways, and doorways count as walkable space.

Each quarter must have at least 55 floor tiles, but rooms are not restricted to those quarters.
Room positions and shapes still change. No room has more than 125 floor tiles,
and at least one has 85 or fewer, so the map is not just four huge rooms.

Walls directly beside floor are useful room boundaries, so they are excluded from the unused-space check.
The largest connected patch of unused tiles behind those walls can contain at most 24 tiles.
If a map fails, generation retries new room positions, sizes, shapes, and routes.
The backup also passes the same rules; it does not accept a mostly empty map.

Generation has a limit on retries. A checked backup layout is used if the random rooms and hallways cannot fit.
One room touches the inside of an outside wall so the existing entrance/exit system still has floor to use.

A new match creates a new layout. `LayoutSeed` is the number used to repeat a layout for testing.
To repeat a map, enable `Use Fixed Seed` on the map component and enter `Fixed Seed` before starting.
Leave it off for normal games. The network host chooses the layout for everyone.
To inspect several layouts safely, leave the match and start a new one, or stop and press Play again.
Do not rebuild only the floor during a running match; players and placed objects need to reset too.

## Placing Things Later

Other scripts can use these methods on `IsometricMapTemplate`:

- `GetTileType(cell)` checks if a tile is a wall or floor.
- `GetRoomType(cell)` gets the room theme.
- `IsHallway(cell)` identifies hallway floor, including door approaches.
- `GetSpaceType(cell)` identifies a small secondary space, or returns `None`.
- `SecondarySpaces` lists their bounds and types without adding major rooms.
- `IsWalkable(cell)` checks if a player can walk there.
- `TryGetRandomFloor(out cell, reserved)` picks an empty floor tile.
- `GetAvailableFloorTiles(reserved)` lists the empty floor tiles.
- `GetAvailableRoomTiles(room, reserved)` lists empty floor inside one room.
- `GridToWorld(cell)` gets a tile's position in the game.
- `TryFindPath(start, end, out path)` finds a walking path.

Keep chosen tiles in a shared `reserved` set so other objects do not use the same spot.
Check that any new obstacles do not block a hallway.

The existing item, event, and obstacle spawners are active in `Race_Gamemode`.
Blue lockers are placed along hallway walls, away from doors. Unsafe placements are skipped.
Rooms have more themed furniture instead of large name labels. Every furniture object
can hold one item, using the existing storage system. Some start empty.
The EXIT door uses the existing win logic.

## Test

Location Switch is a global neutral event. Everyone moves to another player's old
tile, with nobody staying put. Items and unused moves stay with their players.
Destination events do not activate, and the current player can choose a new path.
The turquoise tile is labeled Switch. All 13 events have separate prefabs in
`Assets/Prefabs/Event Tiles/Resources/EventTiles`.
Event rules are listed in `Docs/Events.md`.

The layout test checks 10,002 seeds, 1,000 default furniture plans, and 256 denser scene-style plans. It independently checks protected aisles, wall-mounted whiteboards, newly created dead ends, accessible fronts, pattern variety/weights, density scaling, optional room themes, and repeatability. Run it from the project folder with the .NET 10 SDK:

```powershell
dotnet run --project Tests/SchoolMapChecks/SchoolMapChecks.proj
```

Also check the map in Unity. Start a new match to get a new layout.

For isolated Unity validation, `LocalMultiplayerChecks.Run` accepts `-schoolPlayers 2`, `3`, or `4`, and `-schoolSeed 42` for a repeatable match. Add `-schoolRenderOnly` to validate the scene and save wide, standard, detail, and portrait map renders without running the later battle/input fixtures. PNGs go to `Logs/school-map-seed-<seed>-<size>.png`. These test overrides do not save changes to the real scene. Use a separate project copy when the main editor is open.
