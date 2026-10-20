# Game Mode Components

Each match scene has a named root object for its unique rules:

- **Race_Gamemode > Race Mode > Race Gamemode:** indoor map, optional LAN support, key requirement, before/after-key objectives, escaped-player result, and timeout loss.
- **SurvivalMode > Survival Mode > Survival Mode Controller:** outdoor map, survival objective, countdown-success result, and actual survivor names. There is no exit victory, monster, or elimination system yet.
- Tutorials retain their existing **Tutorial Functionality** script and lesson settings. They are not converted to multiplayer matches.

`GameModeController` extends the team's `GameMode` contract with shared-system references, startup gating, input references and result data. `Race_Gamemode` owns the Race key, key-testing toggles and escape rules; its assigned `RaceKeyBattle` component owns stealing battles. `SurvivalModeController` supplies Survival rules through the same base. `LocalMultiplayerSession` only manages participant setup and match lifecycle. Native UI views present the result supplied by the mode.

## Inspector Setup

Select the named mode object to edit **Shared Match References**, **Match Startup**, the map, objectives and result wording. References are explicitly saved in both scenes; the session does not scan the scene to guess a mode or collect every gameplay script.

**Match Startup > Gameplay Components** contains the enabled components that wait for Start Match. It must include the map, player, inventory, turns and timer. Race event/item/obstacle spawners and key battles are also assigned. UI views manage their own visibility and do not belong in this list. The outdoor obstacle spawner remains enabled: the outdoor generator reads its placement settings while preparing a validated layout before player creation. It does not run an independent gameplay loop.

Timer duration remains on **Turn System > Game Timer > Time Limit**. The timer only counts time; the selected mode supplies the expiration outcome. Player names, colors, count and bot defaults remain on **Local Multiplayer Session**. Movement stays on **Player Character**, marker/ring settings are on **Player Appearance**, and UI styling lives on the saved Canvas prefab. Those shared mechanisms are not copied into competing versions.

**Player Appearance > Editor Preview > Preview In Edit Mode** defaults off. Player sprites and turn indicators stay hidden before Play Mode instead of leaving an isolated white player marker over the camera background. Enable this option only while editing the character visuals.

## Input System

Both modes link **Assets/Settings/MatchInput/SharedMatchInput.asset**. It has Inspector-assigned `InputActionReference` fields for movement, rolling, item use, interacting, item selection, key battles, map pointer/click and shared pause. Each reference links an action by ID in **Assets/Settings/InputSystem_Actions.inputactions**; bindings are edited in Unity's Input Actions editor, not in C# key checks. Duplicate the input profile to assign different actions for a future mode.

**Gameplay Input > Gameplay Input Context** uses the selected mode's profile during a match and an explicitly assigned profile in the standalone tutorial. Player movement/rolling, inventories, item selection, obstacle interaction, inventory hints, key battles and LAN input use those references. The Key Battle action preserves its previous Enter binding. The unused dice demo also accepts an Inspector-assigned action reference.

The teammate's shared **Player/Pause** action from main is preserved: Escape toggles the same pause state in both modes. No Survival-only pause action exists. Native Unity UI buttons also call the existing pause/resume methods. Each scene's **EventSystem > Input System UI Input Module** references the project's existing UI actions for mouse, navigation, submit and cancel.

## Unity UI

Both match scenes instantiate **Assets/Prefabs/Interface/MatchInterface.prefab**. Edit it in Prefab Mode:

The root **Match Canvas Visibility > Preview In Edit Mode** is off by default. The interface stays hidden in the Game view until Play Mode. Enable this option temporarily when editing the Canvas; its objects and references stay authored in Unity either way.

- **HUD View:** turn, player name, die image, bound roll action, countdown and mode objective.
- **Lobby View:** player count, name fields, identity swatches, Race bot toggles and Start button.
- **Pause View:** the shared menu and display toggles. Unity layout automatically compacts modes without display options.
- **Result View:** mode result, survivor names, remaining time and return button. Unity layout expands to fit all names.
- **Inventory View, Storage View, Dice Selection View, Race Battle View:** separate presenters for their respective gameplay components.
- **Event Feedback View:** event notices and a saved Blackout material. **World Markers View** references editable key-badge and storage-assist prefabs.

Item/key reveal hints are off until enabled in the shared pause menu. They use small dark **ITEM** or **KEY** tags, not filled bars. Their prefab RectTransforms control size, and **World Markers UI** exposes offsets, colors, copy and spacing between nearby hints.

Layout, typography, colors, images and persistent button callbacks are saved Unity components. Runtime scripts update data and visibility; they do not construct the Canvas hierarchy or draw IMGUI. Scene references are explicit overrides on **Match UI Context**. UI-only changes should not require editing player, turn, networking or mode rules.

The tutorial keeps its original five lessons: Movement, Items, Lockers, Warp, then Exit. Its native lesson card restores target-aware placement, content-sized text, light keycaps, progress dots and Restart/Skip/Continue/Replay controls. Inventory and pickup notices share the bottom-right layout used by Race and Survival, configured in **Assets/Prefabs/Interface/MatchInterface.prefab**. Lesson copy and targets are editable in **Assets/Settings/TutorialLessons.asset**. **Tutorial View > Tutorial Overlay** exposes placement, progress colors, keycap spacing and the preferred Input Actions binding group. Hints read the linked actions and follow rebinding; keyboard and controller labels are not combined. The completion button loads Race Mode.

## Race Bots and Spawners

Open **Race_Gamemode** and enable **Bot** beside any slot controlled by the computer. **Local Multiplayer Session > Local Player Controllers** exposes the four default bot choices in the Inspector. `SetBotPlayer(index, value)` supports replacement menus. The Race player template has an Inspector-visible `BotController`, disabled until that slot is selected as a bot. Survival does not enable Race bots. Bots use the same seeded turn order, movement, inventory, searchable obstacles and key battles as human players. Network slots remain human-controlled.

**Obstacle Spawner** has separate prefab sections for Computer Lab, Classroom, Hallway, Library and Science Lab, plus Bathroom, Storage Room, Janitor Closet, Study Area, Office and Locker Area under **Special Room Obstacle Prefabs**. Special rooms use only their own section, not a hidden hallway fallback. Existing references migrate once; later Inspector edits stay unchanged. **Item Spawner** has independent weighted item lists per game mode. Race places its one key separately through `Race_Gamemode`; ordinary item types can repeat according to those Inspector weights and the obstacle's item chance. Survival's obstacle settings and empty item inventory remain separate.

**Item Spawner > Ordinary Item Budget > Maximum Ordinary Items** caps filled ordinary containers at four by default, rather than filling approximately half the map. The guaranteed Race key and one reward in each generated special room are separate. Zero disables ordinary items without removing those guaranteed rewards. **Obstacle Spawner > Stored Items > Interactable Chance** still controls the chance for each eligible ordinary container until the budget is reached. Empty containers stay searchable; the number of physical obstacles is unchanged.

## Checks

`LocalMultiplayerChecks.Run` and `SurvivalGameplayChecks.Run` verify scene wiring, unique mode rules, configured action references, stable seeded player order, movement, inventory, timer and results. Survival checks also override the roll binding temporarily: Space must stop rolling and the replacement key must work, without a C# change. `NativeUiChecks.Run -uiScene Race_Gamemode` (also `SurvivalMode` or `Tutorial`) checks real Input System mouse/keyboard interaction and captures wide, standard and portrait views when run outside batch mode. Run checks in an isolated project copy if the main project is open.

`ObstacleCatalogChecks.Run` verifies all six special-room types select their own prefab variants and that Survival area variants do not leak into unrelated sections. `SurvivalMapChecks.Run` checks the saved area catalogs, blockers, walkable spawns, connectivity and material overrides. Cleared sections and rules are never silently repopulated.

`TutorialGameplayChecks.Run` completes all five lessons through real Input System controls, checks rebound keycaps and the shared bottom-right inventory, searches the locker, triggers Warp, exits, and verifies the handoff to the Race lobby. `LocalMultiplayerChecks.Run -itemBudget 0` checks that an empty ordinary-item budget still keeps the key and special-room rewards.

`LocalMultiplayerChecks.Run -lanAuthority` also checks guest authentication, host ownership, items, keys, battles and disconnects. `LanMessageCodec` keeps control packets separate from map snapshots so adding Inspector settings cannot inflate a handshake beyond the host's existing request-size limit.

`LocalMultiplayerChecks.Run -raceBots -schoolPlayers 4 -schoolSeed 42` checks real bot rolls/movement/turn advancement, seeded order and the shared Escape pause action; also accepts 2 or 3 players. Non-batch validation captures the bot lobby and match under `Logs/BotPreviews`.
