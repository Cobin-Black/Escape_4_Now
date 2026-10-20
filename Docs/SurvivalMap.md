# Survival Map Foundation

Open `Assets/Scenes/SurvivalMode.unity`, enter Play Mode, choose 2, 3, or 4 players, and start the match. This scene reuses the indoor mode's player controller, randomized turn controller, individual inventories, inventory bar, countdown, and pause menu. Its shared gameplay HUD shows the current player, round, die/moves, remaining time, and the Inspector-authored objective **Survive the monster.** No item spawner is attached.

Select **Survival Outdoor Map** to edit the **Survival Map** and **Survival Obstacle Spawner** components. The scene is also registered in Build Settings and is separate from the indoor and tutorial scenes.

## Inspector Settings

- **Turn System > Game Timer > Match Settings:** **Time Limit** sets the match duration in seconds (300 by default). The countdown starts with the match, not in the lobby, and freezes while paused. **Survival Mode > Survival Mode Controller** owns the survival-success rule: reaching zero ends the match with **YOU SURVIVED** and a list of every surviving participant's actual name. Edit result wording and survivor heading there; result colors remain on **Local Multiplayer Session**. Race Mode has a separate mode script and retains its timeout loss.
- **Generation Rules:** default 32 x 32 dimensions, internal main-path width, outer circulation width, layout variation, pool deck and track widths, area walkway clearance, validation limits, spawn counts and separation. The original three-band subdivision and two-slot field are retained.
- **Area Rules:** type, required/optional status, appearance chance, width/height ranges, fences, and entrance count. The Track / Field joins two adjacent macro areas and remains at least as large as every other area. Its width/height ranges describe its long/short dimensions; its orientation can change.
- **Pool Water:** minimum water width/height and fill ratio. Defaults reserve at least 4 x 4 water tiles inside an 8+ tile fenced pool area, with clear deck circulation and optional additional deck furniture space. Area sizing accounts for the fence, deck, and water minimum before selecting a plot.
- **Garden and Unused Plots:** garden path width/bend variation and a small courtyard-pocket profile for unused plots. Its chance, size range, and entrance count are editable; set its chance to zero to retain only the main area profiles. Garden routes bend toward seeded interior junctions and join the protected perimeter walkway.
- **Court Style:** tennis probability. Zero selects basketball, one selects tennis, and intermediate values vary between matches. Court surfaces, markings, and equipment follow the selected sport.
- **Parking Layout:** bay width/depth, spacing, and aisle width. Available space determines one or two parking rows. Vehicles align with actual marked bays; other furniture cannot occupy them, and the aisle stays clear.
- **Survival Obstacle Spawner > Obstacle Placement > Obstacle Prefabs:** all 18 outdoor prefab kinds are assigned here, including bushes, bleachers, hoops, tennis nets, lifeguard chairs, light poles, barriers, crates, dumpsters, and sports equipment. Multiple prefabs of one kind give seeded visual variation. Change geometry, offsets, colors, and detail-line strokes on each prefab's **Survival Prop Visual** component. Its **Runtime Rendering > Material Override** optionally assigns a per-prefab material to both geometry and detail lines, including previews. Leave it empty to inherit the map material. The separate material under **Prefab Identity and Preview** is only the fallback for standalone previews.
- **Survival Obstacle Spawner > Placement > Rules:** allowed areas, per-area count ranges, placement chance, footprint, spacing, and attempts. Placement patterns are scattered, edge-biased, clustered, opposite ends, centered, or parking bays. Edge-band width, cluster radius, court-style filter, and movement-blocking override are editable. `None` means unassigned landscaped ground. Automatic blocking preserves the old behavior; an explicit override can make a custom prop blocking or decorative. Main paths, entrances, garden routes, and the central playing field are reserved before placement. An empty rule list stays empty. Disabling the component suppresses props on the next regeneration; terrain fences remain.
- **Seeds and Preview:** random seeds in normal Play Mode; enable **Use Fixed Seed** for reproducible generation. Edit Mode uses **Editor Preview Seed** unless a fixed seed is enabled. **Regenerate Map Preview** generates a new layout with the current settings. The generated seed is shown read-only.
- **Tile Rendering:** assigned shared material, surface colors, tile dimensions, checker contrast, and blocked tile layer. **System References** links the map to its obstacle spawner.
- **Sports / Field and Court / Pool and Parking Markings:** line color/width, track lanes, curve resolution, field inset scale, court inset, basketball keys, tennis service lines, pool lanes, and parking-bay marking inset. These render from the generated area's actual bounds, sport, water rectangle, and parking data.
- **Spawn Preview / Camera Framing:** reservation marker appearance, camera reference, background, padding, and offset.

Generated geometry is temporary and is rebuilt without saving thousands of tile objects into the scene. Prefabs and scene settings are persistent. Failed generation reports a specific Inspector error; an invalid map is never accepted as a fallback. Spawn preview markers appear only in Edit Mode; the real player characters replace them during a match. Regeneration is blocked while a match is running so it cannot invalidate player positions.

**Player 1 > Player Character** owns HUD styling, die size, movement speed, and active-player outline settings. Its mode reference supplies the outdoor map and objective. **Survival Mode > Survival Mode Controller** owns the mode rules, objectives, result wording, shared-system references, and Input System bindings profile. **Turn System > Local Multiplayer Session** owns the lobby title, player colors, pause layout, and display-option visibility. This scene uses same-device play only; indoor-only LAN state is not initialized. The compact pause menu keeps Resume and Leave without indoor event/key hints. **Item Bar** uses the same per-player inventory UI and hides empty inventories, matching the first mode. Its **Hide When Empty** setting remains editable in the Inspector. See `Docs/GameModes.md` for the script boundaries and input wiring.

## Map Data

`SurvivalMap.Layout` exposes the seed, dimensions, area/zone lookup, entrances, court styles, courtyard-pocket flags, water rectangles, parking bays, prop footprints/rotation/blocking, player reservations, and clearance-tested spawn candidates. The layout remains pure C# and does not depend on scene objects.

- `IsWalkable(Vector2Int)` is the authoritative collision query.
- `IPlayerGridMap` is the shared movement/setup contract; both maps use the same cardinal shortest-path implementation. Occupancy tracks real players, including shared tiles, independently of blocking obstacles.
- `GridToWorld` and `WorldToGrid` convert between grid and scene positions.
- `TryGetPlayerSpawn` reads a valid player reservation.
- `GetSpawnCandidates(minimumDistanceFromPlayers)` excludes reserved and currently occupied player cells and applies separation to both. It can support a future monster spawner without implementing one now.
- `Layout.GetNeighbours` provides cardinal walkable neighbours for future navigation.
- `GenerateWithSeed` rebuilds the complete map deterministically.

Every water, fence, and blocking prop tile has a corresponding `PolygonCollider2D`. Flood-fill connectivity, maximum dead-end length, and an iterative main-route articulation-point check use this same blocked grid. A layout that depends on a single main-route tile is rejected and regenerated. Small non-main-path dead ends still use the configured limit. No separate visual-only obstacle map exists.

## Validation

Run `dotnet run --project Tests/SurvivalMapChecks/SurvivalMapChecks.proj -- 1000` for independent connectivity and route checks, both court styles and matching equipment, water minimums, protected sports/garden space, courtyard pockets, parking alignment, all prop kinds, blocking overrides, optional chances, spawn clearance, repeatable seeds, alternate dimensions, and bounded failure with impossible settings.

Unity's **Tools > Survival > Validate Scene and Render Previews** validates every serialized prefab kind, conversions, colliders, and camera framing at wide, standard, and portrait resolutions, plus representative campuses with basketball/tennis, pools, and maintenance. It writes actual renders under `Logs/SurvivalPreviews` with their seeds in the Console. It can also run with `-executeMethod SurvivalMapChecks.Run` in a separate editor.

**Tools > Survival > Validate Playable Match** checks actual input, valid players and inventories, seeded order over two rounds, movement/occupancy, active-player outlines, pause/resume, lobby clock gating, countdown, and expiry. Batch command: `-executeMethod SurvivalGameplayChecks.Run -survivalPlayers 4` (also accepts 2 or 3). It does not save test changes to the scene. Use a separate project copy for batch testing when the main project is open.

## Scope

This is a playable map/gameplay foundation, not the finished Survival rule set. It intentionally does not add a monster, monster spawning, combat/co-op rules, or indoor-mode events/items. The existing inventory logic is ready for a different future item set. Until the monster is added, all players survive if the countdown completes; players can also leave through Pause.
