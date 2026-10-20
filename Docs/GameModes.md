# Game Mode Components

Each match scene has a named root object for its unique rules:

- **Race_Gamemode > Race Mode > Race Gamemode:** indoor map, optional LAN support, key requirement, before/after-key objectives, escaped-player result, and timeout loss.
- **SurvivalMode > Survival Mode > Survival Mode Controller:** outdoor map, survival objective, countdown-success result, and actual survivor names. There is no exit victory, monster, or elimination system yet.
- Tutorials retain their existing **Tutorial Functionality** script and lesson settings. They are not converted to multiplayer matches.

`GameModeController` extends the team's `GameMode` contract with shared-system references, startup gating, input references and result data. `Race_Gamemode` owns the Race key, key-testing toggles, key battles and escape rules. `SurvivalModeController` supplies Survival rules through the same base. `LocalMultiplayerSession` sets up participants and displays the result supplied by that component.

## Inspector Setup

Select the named mode object to edit **Shared Match References**, **Match Startup**, the map, objectives and result wording. References are explicitly saved in both scenes; the session does not scan the scene to guess a mode or collect every gameplay script.

**Match Startup > Gameplay Components** contains the enabled components that wait for Start Match. It must include the map, player, inventory, turns and timer. Race event/item/obstacle spawners and the inventory HUD are also assigned. The outdoor obstacle spawner remains enabled: the outdoor generator reads its placement settings while preparing a validated layout before player creation. It does not run an independent gameplay loop.

Timer duration remains on **Turn System > Game Timer > Time Limit**. The timer only counts time; the selected mode supplies the expiration outcome. Player colors remain on **Local Multiplayer Session**, while movement, HUD styling and active-turn outline settings stay on **Player Character**. Those shared mechanisms are deliberately not copied into two competing versions.

## Input System

Both modes link **Assets/Settings/MatchInput/SharedMatchInput.asset**. It has Inspector-assigned `InputActionReference` fields for movement, rolling, item use, interacting, item selection, key battles and map pointer/click. Each reference links an action by ID in **Assets/Settings/InputSystem_Actions.inputactions**; bindings are edited in Unity's Input Actions editor, not in C# key checks. Duplicate the input profile to assign different actions for a future mode.

Player movement/rolling, inventories, item selection, obstacle interaction, inventory hints, key battles and LAN map input use this profile during matches. The Key Battle action preserves its previous Enter binding. Existing standalone/tutorial components retain their project-action fallback for compatibility. The unused dice demo is not part of either match scene and was not refactored.

The teammate's shared **Player/Pause** action from main is preserved: Escape toggles the same menu in both modes. No Survival-only pause action exists. Existing temporary IMGUI lobby/HUD/result presentation is retained; this does not replace it with a new Canvas UI.

## Race Bots and Spawners

Open **Race_Gamemode** and choose **Human** or **Bot** beside each active lobby slot. **Local Multiplayer Session > Local Player Controllers** exposes the four default bot choices in the Inspector. `SetBotPlayer(index, value)` supports replacement menus. Race enables the merged `BotController`; Survival does not enable Race bots. Bots use the same seeded turn order, movement, inventory, searchable obstacles and key battles as human players. Network slots remain human-controlled.

**Obstacle Spawner** now has separate prefab sections for Computer Lab, Classroom, Hallway, Library and Science Lab. **Item Spawner** has independent weighted item lists per game mode. Race places its one key separately through `Race_Gamemode`; ordinary item types can repeat according to those Inspector weights and the obstacle's item chance. Existing Survival obstacle settings and empty item inventory remain separate.

## Checks

`LocalMultiplayerChecks.Run` and `SurvivalGameplayChecks.Run` verify scene wiring, unique mode rules, configured action references, stable seeded player order, movement, inventory, timer and results. Survival checks also override the roll binding temporarily: Space must stop rolling and the replacement key must work, without a C# change. Run checks in an isolated project copy if the main project is open.

`LocalMultiplayerChecks.Run -lanAuthority` also checks guest authentication, host ownership, items, keys, battles and disconnects. `LanMessageCodec` keeps control packets separate from map snapshots so adding Inspector settings cannot inflate a handshake beyond the host's existing request-size limit.

`LocalMultiplayerChecks.Run -raceBots -schoolPlayers 4 -schoolSeed 42` checks real bot rolls/movement/turn advancement, seeded order and the shared Escape pause action; also accepts 2 or 3 players. Non-batch validation captures the bot lobby and match under `Logs/BotPreviews`.
