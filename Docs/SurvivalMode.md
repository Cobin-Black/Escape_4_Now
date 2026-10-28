# Survival / Monster Mode Rules

Use the existing `SurvivalMode` scene. The scene integrates the existing `MonsterCharacter`, skins and dice presentation from `First_Sprites_Branch`; the Survival rules remain responsible for shared puzzles, escape, participant outcomes and safe procedural placement. Monster movement and abilities remain in their separate controller and use the imported Monster Input System actions.

## Match Loop

Complete shared puzzles, unlock both evacuation points, then physically reach an unlocked point. Each escaped survivor keeps their win. Death or timeout loses for that survivor, without undoing completed objectives or another survivor's escape. The match ends when every survivor has escaped/died, or the clock expires.

The default clock is 600 seconds, configured on **Turn System > Game Timer > Time Limit**. It continues during puzzles. The existing shared Pause action/menu pauses it. Race's timer and win conditions are unchanged.

With no Monster slot selected, the lobby uses the existing AI monster and supports 1-4 survivors. Select **Monster** beside any lobby slot for 1-3 survivors versus a human-controlled monster. For survivor-only testing, disable **Spawn AI Monster When Unassigned** on the session. Names, colors and chosen sprite skins retain their original lobby slots; the selected monster slot is excluded from the shared puzzle budget. Task budgets use the initial survivor count, not the remaining count:

| Survivors | Required | Stations |
| --- | --- | --- |
| 1 | 2 | 3 |
| 2 | 3 | 4 |
| 3 | 4 | 5 |
| 4 | 4 | 6 |

## Unity Settings

- **Survival Mode > Survival Mode Controller:** map, shared systems, rules asset, task/event components, setup roles and optional external actor reference.
- **Assets/Settings/Survival/Match Rules.asset:** required/extra counts, compatible puzzle pool, exit count/separation, interaction range, protection/spawn clearances, placement/retry limits and HUD/result copy.
- **Assets/Settings/Survival/[Puzzle Type].asset:** enabled state, prefab, compatible areas, difficulty, matching colors/symbols and memory flash timing.
- **Assets/Settings/Survival/Event Settings.asset:** enabled events, categories, placement/random-effect weights, amounts, durations, Blackout turns by survivor count, temporary-effect prefabs and feedback text.
- **Assets/Prefabs/Survival/Gameplay:** editable stations, exits, event/area/branch markers and the native **Survival Interface** prefab. Scene references are assigned on its instance inside **Match Interface**.
- **Survival Outdoor Map:** the existing map and obstacle generation values remain authoritative. Fixed seed controls campus, objectives, event placement, starting order and puzzle-attempt sequences.

## Campus Presentation

**Survival Outdoor Map > Survival Campus Visuals > Settings** references **Assets/Settings/Survival/Campus Visuals.asset**. Surface palettes, optional materials, coherent grass variation, field mowing stripes, pavement joints/edging, pool coping/drains, courtyard patterns, planting beds, flower density/scale, garden trails and rain treatment are editable there. **Detail Density** changes small pavement details without adding gameplay obstacles. Switching the component off restores the legacy surface rendering; it never changes area placement, physics, movement costs or the walkable grid.

The obstacle spawner still owns logical prop counts and patterns. Its area prefab lists select cosmetic variants using the map seed: courtyard seating groups, pool lounges, equipped sheds, track storage and vehicle colors reuse the existing prop kinds/footprints. **Area Fence Styles** supplies sports and service fences; the outer boundary retains its original containment grid. Flowers are confined to existing planted prop footprints. Garden trails follow existing walkable routes rather than carving new paths.

Each puzzle definition now points to its own editable station prefab with visual equipment details. On **Survival Task Controller**, **Show Task Debug Labels** reveals all unfinished tasks; when off, **Show Nearby Task Names** and **Task Name Distance** control names near the active survivor. Interaction can still show its current task name. Completed task names remain hidden. The shared Pause toggle controls the global debug-label option. Exit gate signal lights change from locked to unlocked alongside the existing physical gate state; puzzle requirements remain authoritative.

Rain uses the existing turn-based event and particles. The campus presentation layer adds a temporary pavement tint and sparse puddle highlights while that weather is active. The existing Mud, Sprinklers, Branch, Car Alarm, Track Boost and Poolside Slip triggers remain separate from ordinary event tiles.

## Puzzles and Turns

Use the existing configured Interact action or contextual task button while standing on a valid interaction tile. Interaction is allowed before rolling or with unused movement. It locks that player's movement/item input, not the clock. Solve, fail or leave the puzzle to end the turn. A cancelled/failed attempt has no saved partial progress; a solved station stays complete for the team.

If movement runs out beside an unfinished task, the survivor can still choose Interact. Solving or leaving the puzzle finishes the turn. The optional End Turn button is currently hidden; its logic remains available through the turn controller's Inspector setting. Otherwise the existing automatic turn ending remains. The single seeded turn order repeats for the match; retired actors are skipped without changing participant identities or reshuffling.

The pool contains wires, button sequence, circuit rotation, code entry, power switches, supply sorting, number arrangement, battery matching and breaker combination. Prefer distinct types in a match. Matching supports click/navigation and drag/drop; number tiles support swapping and drag/drop. Circuit generation guarantees a solvable path. All information is shared-screen, not private to a role.

## Placement and Events

Objective placement overlays the existing generated campus rather than replacing its generator. Tasks use compatible generated areas, avoid existing props and spawn/exit reservations, and reserve valid interaction spaces. Flood fill rejects disconnected layouts. Invalid objective layouts retry; impossible Inspector settings reject match startup instead of silently lowering the objective count.

Two separated, marked evacuation points use walkable perimeter circulation. They unlock together and remain usable after earlier survivors escape. Task objects are blocked in the authoritative map grid; adjacent interaction spaces remain walkable. The extra station budget and order-independent completion let survivors abandon a camped objective.

Events avoid all protected objective/spawn/exit cells. Warp/Area Switch avoid occupied cells, all events and reservations and never trigger a second event. Position switches use only active survivors' existing positions and preserve monster occupancy, inventory and team progress. Forced-teleport tiles must have bypass routes. Fallen Branch validates connectivity with both existing temporary blockers and forced-movement hazards before blocking a tile; unsafe effects are skipped.

Survival has separate trigger groups. Each seeded match selects a weighted subset of enabled types: initially 4–6 standard types, 2–4 environmental types, 1–2 possible global types, and optionally Energy Drink. The floor budget rolls 6–10 hidden tiles, capped by available safe space, 2% density, spacing and per-type quotas. **Show Event Tiles** reveals only that group. Environmental objects and weather stay visible independently of that toggle. Selection does not guarantee activation: missing areas, unsafe placements, scheduler chance or the match ending can prevent an event.

| Trigger | Events and behavior |
| --- | --- |
| Standard tiles | Lucky Roll, Adrenaline Rush, Second Chance, Trip, Exhausted, Bad Roll, Freeze, Warp, Area Switch, Random Event, Location Switch. |
| Ground and areas | Mud is a connected temporary grass patch outside protected routes. Sprinklers affect only safe cells inside a compatible generated area. Both add traversal cost without blocking movement. |
| Movement context | Poolside Slip rolls its configured chance only on visible puddle patches on safe Pool deck tiles. Track Boost uses marked sections of actual Track surface and grants its bonus once per actor turn. |
| Physical objects | Fallen Branch blocks a validated alternate walkway until expiry. Car Alarm attaches particles and looping audio to an actual generated Parking car; it never directs a monster. |
| Pickup | Energy Drink is a visible can using existing inventory capacity, automatic step collection, and the shared Use Item action. Using it before rolling saves a bonus for the following turn. |
| Global | Blackout, Rain Storm, School Bell, and Location Shuffle are chosen by the seeded scheduler without activation tiles. Rain has native weather particles and a configurable roll penalty. School Bell plays its sound and saves each survivor's next-turn bonus. Shuffle exchanges only survivors' existing positions. |

Intervals are configurable ranges of completed actor turns, with separate global/environmental chances and per-event weights. Durations also use completed actor turns. Scheduling stops while paused, audio pauses with the shared menu, and unsafe or unavailable effects are skipped. Optional areas are never forced into a map. Freeze does not stack; Second Chance blocks one negative player event, not global weather or terrain placement.

In **Additional Event Settings > Per-Match Variety and Density**, edit the type-count ranges, tile budget, density and spacing. Each named event's **Instances Per Match** range limits floor placements, installed sections/pickups or successful scheduled activations according to its trigger. Temporary effects do not regain their quota when they expire. Blackout, Freeze, Fallen Branch and Location Shuffle ship with lower weights and at most one occurrence; their zero minimum allows omission. Use **Use All Enabled Events For Debugging** only for controlled tests. It still respects compatibility, safety and upper occurrence limits. `SurvivalMatchEventPlan` holds match-specific selection and usage without modifying the settings asset.

Open Gate remains disabled: the current functional gates are puzzle-locked exits, so an event must not bypass the objective requirement. No temporary shortcut gate or monster attraction behavior is invented.

## Monster-Team Integration

The scene's session and HUD reference the existing **Monster** object. Selecting its lobby role initializes that controller with the generated map and survivors. The reserved footprint is configured in **Match Rules > Monster Footprint**, currently 2×2 to match the incoming controller. All footprint cells are protected from tasks and events, and current monster occupancy is excluded from environmental placement and warp destinations. The enemy turn is inserted once into the seeded fixed rotation and counts toward turn-based event scheduling and expiry. The health system's elimination is reflected in Survival outcomes without resetting completed puzzles. Surviving the timer alone remains a loss under these puzzle-and-escape rules.

Before Start Match, assign any setup slot with `SetSetupRole(slot, SurvivalRole.Monster)` or the Inspector role list. This supports 1-3 survivors plus one player-controlled actor; Player 4 is not special.

For four survivors plus a future externally created AI actor, assign **External Monster** or call `ConfigureExternalMonster(actor)` before starting. The existing actor is configured against the shared map and reserved spawn, then appended to the initial seeded turn roster. This does not instantiate, enable or implement an AI controller. The monster team owns that actor and its behavior. Configure only one monster source.

`PlayerCharacter.TakeDamage()` retains the incoming health/damage behavior. Survival observes zero health and calls `MarkSurvivorKilled(player)` to retire movement/interaction, cancel any open puzzle, remove future turns and preserve shared completion. `OutcomeOf`, `RoleOf`, `ActiveSurvivors`, `ActiveTurnParticipant` and `Objectives.MonsterSpawn` expose the relevant state. Monster abilities and damage amounts remain owned by the monster controller.

## Script Responsibilities

- `SurvivalModeController`: roles, lifecycle, individual outcomes and exit eligibility.
- `SurvivalTaskController`: interaction, attempt lifecycle and station visuals.
- `SurvivalRulesSettings` / `SurvivalPuzzleDefinition` / `SurvivalEventSettings`: Inspector configuration.
- `SurvivalObjectiveLayout` / `SurvivalEventLayout`: seeded placement, reservations and route validation.
- `PuzzleSession`: transient puzzle algorithms; no Unity UI or match victory logic.
- `SurvivalPuzzleUI` / `PuzzleTokenUI`: authored native UI presentation and pointer/drag interaction.
- `PuzzleKeypadUI`: native code-keypad entry, delete and clear controls, referencing the authored InputField.
- `SurvivalEventController`: standard tile effects, shared event messaging and global-effect duration.
- `SurvivalEnvironmentController`: protected environmental placement, physical pickups, contextual Track/Pool triggers, terrain costs and temporary obstacle expiry.
- `SurvivalEnvironmentalPlacement`: reusable eligibility and connected-patch algorithms on generated grid data.
- `SurvivalEventScheduler`: independent seeded global/environmental schedules and weighted safe fallback selection.
- `SurvivalAmbientEffect`: authored particle/audio presentation, map-sized weather coverage, Pause and expiry handling.
- `EnergyDrinkItem`: Survival pickup using the existing inventory and Use Item action.
- `IMapEventSource` / `SharedEventEffects`: shared event presentation contract and common Race/Survival effects.

## Validation

Environmental ground regions now render as a single continuous mesh with rounded outside corners and shallow irregular puddles. No internal tile borders are drawn. **Mud Patch**, **Wet Pool Deck**, **Track Boost Marking** and **Sprinkler Water** expose their appearance through `SurvivalEnvironmentalSurface`: material, palette, edge rounding, puddle settings and track paint size/width. Their old tile visuals are disabled. Ground geometry stays inside the selected safe cells and adds no colliders.

**Event Settings → Environmental Placement** controls track-section spacing, straight-section preference, clearance from large track props, proximity of wet deck regions to water, preference for landscaped mud, and sprinkler head count/spacing. Sprinklers affect a connected patch with a few spaced heads. Track chevrons follow the actual oval's tangent and fit within its lanes. Mud remains off protected routes; puzzle/spawn/exit reservations and deterministic seeds still apply.

Survival notifications follow the actual effect state: pending roll/movement effects stay until consumed, Second Chance until used, and environmental/weather messages until their effects expire. Immediate results use **Event Settings → Feedback → Immediate Notification Seconds**; Pause stops notification aging. **Visible Notifications** limits HUD clutter while retaining other active effects. Outdoor activations use the same feed as standard tiles. Race feedback is unchanged.

Outdoor effect prefabs contain editable native Particle Systems, surface highlights, branch detail and car alarm lights. `SurvivalEffectVisual` handles cosmetic animation; `SurvivalAmbientEffect` fits weather to the generated campus and pauses particles/audio. Station prefabs use `SurvivalTaskBeacon` for a nearby interaction ring. The existing Interact action opens tasks; the prompt reads its binding from the Input System and explains the turn cost. Adjust the prompt and button in **Survival Interface**, and the highlight on each station prefab. These visuals do not add colliders or modify protected placement.

Puzzle windows share the editable **Survival Interface** prefab: centered token captions, rounded controls, a cyan frame, contrast-aware text and separate instruction/progress areas. Arrange Numbers uses a single left-to-right row; select two tiles to swap or drag one onto another. Its target order and selected-tile prompt are shown explicitly, and swaps clear stale selections. Circuit pieces rotate without starting token drags. Matching tasks accept either click pairs or dragging. The code puzzle includes a native keypad with **DEL**, **CLEAR**, and **Confirm code** controls. Four-control layouts use a balanced grid. Sizes, colors, spacing limits and feedback strings remain Inspector settings.

`SurvivalRulesChecks.UI` fully solves all nine puzzles through native Input System UI controls, checks wrong wire/sequence/code attempts and number cancellation, and captures desktop/portrait layouts. Add `-puzzleType ArrangeNumbers -puzzleCount 3` (also 4, 5, 6) for the supported number counts; `-puzzleSeed` selects the attempt seed.

### Visibility and Input

The two escape gates are placed at random reachable positions on opposite campus edges using the match seed. Their protected approach tiles remain inside the playable map. Each gate replaces its perimeter fence section and starts closed with a **LOCKED** label. Reaching the shared puzzle requirement opens both gates and changes their labels to **EXIT**. Survivors escape individually by reaching an unlocked gate's approach tile; both exits remain open for the remaining survivors. No extra input binding is needed for escape.

The **Evacuation Point** prefab holds `SurvivalExitGate`, with separate editable **Closed Gate Geometry**, **Open Gate Geometry**, and label references. Gate dimensions, parts, colors and status wording are Inspector settings. Existing **Match Rules** settings control exit count and minimum separation; exits are selected in opposite-side pairs.

The shared Pause menu offers **Show Event Tiles** and **Show Task Names** in Survival. Task labels use each puzzle definition's editable short map label; hiding labels leaves the physical stations and their interactions in place. Event visibility changes only marker renderers, so hidden events still trigger normally. Race retains its event/item/key display options.

Task names use white text on fitted dark plates. Event diamonds are filled with a dark outline: green `+` is positive, red `!` is negative, blue `?` is neutral, and purple `*` is global. These colors, symbols, sprite references, text sizes and task caption padding are editable in the Survival marker prefabs. **Caption Readability** on `SurvivalWorldMarker` holds the optional background and caption color override; hiding task names hides their plates too.

Each puzzle definition has a short **Map Label** such as **Wires**, **Sequence**, or **Numbers**. This compact name is used above stations; the full **Title** remains in the puzzle window. Leaving **Map Label** empty falls back to the full title. Text size and background padding are configured in the **Puzzle Station** prefab.

Each puzzle type can contribute only once per match. Solving one station completes every station with the same `SurvivalPuzzleType`, hides their names and backing plates, and prevents further attempts at those stations. The station objects remain on the map. Turning **Show Task Names** off and back on reveals only unfinished stations. The generator requires at least one extra distinct type beyond the required count, including when a restricted Inspector pool causes duplicate stations to appear.

Select the Survival map's **Survival Event Controller** to edit its assigned settings inline under **Event Spawner Settings**. **Event Prefabs** lists the named prefab references, and **Spawn Chances** lists enable toggles and relative weights. **Event Details / Add or Remove Events** holds the full named catalog, including compatible areas, bonuses, penalties and durations. **Additional Event Settings** holds global movement, blackout and feedback settings. The same settings remain editable by opening the **Settings** asset directly. Inline edits update that shared asset. Race keeps its existing prefab/chance arrays, with chance entries labeled by event name and percentage. On **Survival Task Controller**, use **Task Labels** for the initial label visibility and name format. Caption position, size and colors live in the **Puzzle Station** prefab; puzzle names live in the puzzle definition assets.

Player actions reuse the project's Input Actions, including Interact and shared Pause. Native menu toggles and puzzle pointer/drag/submit controls run through the existing `InputSystemUIInputModule`. Procedural event spawning and stepping onto event tiles are automatic gameplay behavior and do not need separate input actions.

Use an isolated project copy if Unity already has the main project open.

- `-executeMethod SurvivalRulesChecks.Generation`: seeded campus/objective/event connectivity, compatibility, reservation and puzzle solvability checks.
- `-executeMethod SurvivalRulesChecks.Run -survivalPlayers 1` (also 2, 3, 4): playable rules, shared completion, cancellation, timer/Pause, effects, escape and individual results.
- Add `-expiryOnly true` for timeout during an open puzzle; `-allDead true` for all-eliminated results; `-monsterSlot 0` with two participants for role assignment; `-externalMonster true` with four survivors for an externally supplied turn-slot fixture.
- Add `-eventTriggers true` for actual terrain costs, safe branches/expiry, Track/Pool triggers, Energy Drink input, global schedules, weather/audio, Pause and monster-position preservation. It supports survivor counts 1-4 and the external-actor fixture.
- `-executeMethod SurvivalRulesChecks.UI`: real native pointer/drag controls for all nine puzzle views, shared Pause and desktop/portrait captures.
- Existing Race, bot, LAN and tutorial checks remain regression coverage for the reused systems.
