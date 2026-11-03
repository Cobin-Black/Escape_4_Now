# Free For All and Hot Potato

Open `Assets/Scenes/FreeForAll.unity` or `Assets/Scenes/HotPotato.unity`, or select the mode in Main Menu. Both support 2–4 local players. The room is generated after Start Match; it does not generate while editing or in the lobby.

## Controls

Each lobby row chooses a keyboard layout or one specific connected gamepad. A gamepad can belong to only one player. Keyboard layouts are independent, so everyone can move together.

| Slot | Movement | Tag / pass |
| --- | --- | --- |
| 1 | WASD | F |
| 2 | Arrow keys | Right Ctrl |
| 3 | IJKL | O |
| 4 | Numpad 8/5/4/6 | Numpad 0 |
| Assigned gamepad | Left stick | South button |

Edit these bindings in `Assets/Settings/Arena/Arena Controls.inputactions`. Pause uses the existing project `Player/Pause` action. UI uses `InputSystemUIInputModule` with the existing project UI actions. No dice actions or turn controllers run in these scenes.

## What belongs where

- `ArenaMatch`: local match startup, names, colors, skins, Pause and shared last-player result.
- `ArenaParticipant`: continuous movement, circle collision, temporary movement effects and elimination. It uses the existing `PlayerCharacter` identity, appearance and health.
- `ArenaLocalInput`: one private action copy per participant, restricted to their keyboard map or paired gamepad.
- `ArenaLayout`: connected room data and safe, separated spawn selection.
- `ArenaMap`: isometric rendering with existing blocks, materials and prop prefabs.
- `ArenaSharedCamera`: follows and frames the living players on one screen.
- `ArenaEventDirector`: common real-time event selection and scheduling. Each mode has its own saved event pool; these are not pickups.
- `ArenaUI`: switches setup, gameplay, pause and result screens; it does not own game rules.
- `ArenaLobbyView`: player-count buttons, names, controls, skin choices and portraits.
- `ArenaHudView`: compact mode information, Hot Potato clock/holder and all-player health.
- `ArenaHealthRow`: one participant's portrait, identity color, health segments and eliminated state.
- `ArenaObstaclePlan`: seeded cover groups, protected center routes and connectivity checks.
- `FreeForAllController`: last player standing; no attacks or final combat rules are implemented.
- `HotPotatoController`: passing, countdown, elimination and successive rounds.
- `PotatoHolderView`: attached potato, countdown, flashing and ticking.

The `realTimeMovement` flag is enabled only on the saved Arena Player prefab. It bypasses `PlayerCharacter`'s dice input and grid setup. Existing Race, Survival and Tutorial players keep the flag off. Their normal movement code is retained.

## Unity settings

Select each scene's mode object for player, map, input, skin and event references. Hot Potato exposes round length, pass distance, cooldown, round delay, optional contact passing and repositioning between rounds.

`Assets/Settings/Arena/FreeForAll Map.asset` and `HotPotato Map.asset` expose room size, aisle width, service-room depth, equipment ranges, prop references, materials, colors, seed options, spawn distance, clearance and camera settings. The gym keeps its central court. The auditorium keeps a stage, seating blocks, center and side aisles, and connected backstage rooms. Randomness varies door openings, equipment, event locations and spawn choices.

`FreeForAll Events.asset` and `HotPotato Events.asset` expose enabled environmental events, weights, warnings, cooldowns, durations and concurrency. Items are separate. See [ArenaEvents.md](ArenaEvents.md) for the supported effects, reuse details and safety rules.

Both rooms now default to 20×20. `Room Visuals.asset` controls editable gym and auditorium details, while `Event Visuals.asset` controls warnings, lighting, wet ground, balls and effect presentation.

### Interface and cover editing

`Interface Style.asset` contains the shared palette, typography, panel widths, spacing, row heights and timer warning threshold. The setup and HUD are saved Unity objects under **Arena Interface**, with normal layout components and serialized widget references. Teammates can edit these objects directly. Changing style geometry defaults requires running **Tools / Escape 4 Now / Polish Arena Interface** to rebuild the saved layout; this replaces the arena UI, so save any manual UI edits first. Runtime color and warning values use the style asset directly.

The setup grows with the selected 2/3/4 player count instead of reserving empty rows. Health is inside the top-left HUD, with a separate card for every player. Hot Potato adds round, countdown, timer bar and current holder; the holder's health card gets a gold outline. Pause remains in the top-right. The shared camera accounts for the actual HUD width when framing living players.

Each map has a separate **Obstacle Groups** asset, referenced by its map settings. These expose group count, maximum blocked tiles, floor coverage, placement attempts, spacing, route width, and weighted patterns. Each pattern contains editable prop prefabs and tile offsets, a compatible floor zone and clearance. Existing ball carts, shelves, benches, speakers, crates and control desks are reused. Stacked mats, cones and folding partitions are saved native block-model prefabs with editable parts and colors.

The gym places sports cover groups around open court routes. The auditorium uses stage equipment, aisle furniture and partitions around its existing seating blocks. Groups rotate and move between seeds. A proposed group is rejected if it disconnects the room or creates a new dead end beside it; spawn selection happens afterward against the resulting collision data. An oversized or incompatible pattern may be skipped. Neither map needs escape doors. Floating area names and scoreboard text are disabled in the saved settings; player names and the potato marker remain visible.

`ArenaPresentationAuthoring` owns saved UI construction; `ArenaObstacleAuthoring` owns cover palette/prefab defaults. They are editor tools, separate from the runtime components and mode controllers. The arena C# files use expanded formatting and short plain-English comments explaining responsibilities and the less obvious rules.

Select the Arena Player prefab for movement speed, collision step, name label and the existing Health field. The UI, materials, props, skins and potato graphics are saved Unity objects and can be edited normally.

## Combat and future extensions

Future combat can call `ArenaParticipant.Damage(amount)`, `Knockback(gridDirectionAndDistance)` or `Eliminate()`. Damage uses the existing player health; reaching zero triggers the shared elimination rules. Eliminating a participant disables movement and removes them from targeting and camera framing. The remaining players continue.

For manual Free For All testing, the participant component's **Testing / Eliminate Player** context action is available during Play Mode. There is no invented attack button.

Hot Potato tags use proximity plus a clear line through walkable map data. Contact alone does not pass by default. A pass never resets the round countdown. Expiry eliminates the holder; temporary effects clear before the next round. Living players receive safe separated positions and a randomly selected holder. Both the countdown and round delay stop during Pause.

`ArenaChecks.Generation` validates hundreds of seeded rooms, connectivity, spawn separation and deterministic generation. `ArenaChecks.Run` exercises saved scenes, simultaneous input, collision, elimination, Pause and round progression. Run these in a separate validation copy if your editor has unsaved scene changes. `ArenaAuthoring.Build` is a regeneration tool, not a required runtime step; it replaces the initial saved arena defaults and should not be used to overwrite designer changes.

`ArenaPresentationChecks.Run` tests native pointer selection, setup resizing, Start/Pause/Resume, portraits, independent player health, compact HUD bounds, removed area labels, real cover groups and a narrow portrait viewport. It saves preview screenshots in `Logs/ArenaPresentationPreviews` inside the validation project.

## Validation of this foundation

400 seeded room layouts passed connectivity and safe-spawn checks for 2, 3 and 4 players. Play checks passed for keyboard movement, isolated gamepad input, collision, HUD-safe camera framing, Pause, elimination, tagging and every supported round count. The existing Race UI/gameplay checks and Survival shared-puzzle/gameplay checks also passed.

The older Survival native UI suite stops on its fixed-height HUD assertion. The existing HUD already adds height for survivor health and monster abilities, which that assertion does not account for. Neither the Survival HUD nor its old test was changed for these modes.

## Map, movement and Inspector refinement

Both arena maps now expose their assigned map settings directly in the map component's Inspector, grouped into generation/seed, room layout, player spawns, obstacle prefabs, materials, auditorium settings, camera, and debug labels. Expand **Obstacle Groups** for density, clearances, pattern offsets and facing rules. Expand **Room Details** for reusable art prefabs. These edit the assigned assets; the gym and auditorium retain separate map and obstacle assets.

Props use logical facings toward the room, stage or audience, or a designer-selected fixed quarter-turn. Cover groups prefer the edges of the play space. Auditorium chairs form two balanced blocks with short row sections, a center aisle, side aisles and cross aisles. Row spacing, section depth and block width are editable. Chairs face the stage; stage speakers face the audience. All proposed blockers still pass clearance, connectivity and dead-end checks. Spawns are chosen after this placement.

The camera defaults to framing the whole generated room, including prop headroom. It can gently widen with player spread, but does not zoom in so far that important routes disappear. Camera padding, safe viewport, response and zoom limits are editable on each map asset. Fitting the map takes priority over the preferred maximum zoom on unusually narrow screens. Player-follow framing remains an Inspector option. The HUD stays compact in the unused upper corner instead of reserving an empty strip along the entire left side.

Arena movement speed is measured in visible world units per second. The input is clamped before inverse isometric projection, so horizontal, vertical and diagonal screen directions have equal speed. A complete step is tested first; blocked movement slides along screen axes. Ordinary input stops immediately when released; the existing Slippery Floor effect can still intentionally add inertia. These changes apply only to arena participants.

Floating player names default off. **Arena Player / Arena Player Indicator** exposes colored ground-ring radius, width and holder pulse settings. Hot Potato adds a gold outer ring while retaining the overhead potato/countdown and HUD holder highlight. Identity colors, skins and health still belong to the existing player system.

The HUD uses the existing filled/dimmed heart style through the shared `HealthHeartText` formatter. Survival calls the same formatter with its original colors; no health rules changed. **Interface Style** controls red heart color, dimmed-heart color, HUD scale, width and spacing. Layout defaults are saved into the scene by the UI authoring tool; the actual UI components remain directly editable in Unity.

**Show Events** is disabled by default in both arena scenes. Enable **Show Event Button** on the Arena UI component only when needed for debugging; its logic is retained. When enabled, it changes map assistance only. It reads `ArenaEventDirector`'s existing active event states and displays small location rings in their existing event views. It never disables events, timers, environmental visuals or safety warnings. There is no second event registry. The button uses the existing Input System UI module; no raw key polling or new pause action was added.

## Explosion, wall fixtures and event discovery

Select **Procedural Gymnasium** or **Procedural Auditorium** in the scene. The map Inspector includes **Event Spawner / Enabled Events and Weights**. This edits the existing mode-specific event pool directly: enabled toggles, weights, definitions, timing, limits, audio and visual references. The same controls are on the mode object's **Arena Event Director** component. These modes mix environmental/global events, so a floor prefab is not required for every definition.

Events remain enabled. Their initial delay defaults to 5–8 seconds and their locations are selected at activation time. **Show Events** reveals live indicators; while nothing is active, the HUD can show the next attempt countdown. A placement attempt may be skipped if it cannot be made safe. Per-match limits still apply and are editable in each event definition.

Hot Potato emits a presentation signal before hiding the eliminated holder. `PotatoExplosionView` instantiates the saved **Potato Explosion** prefab at that position. It reuses `SurvivalWorldBurst` with optional arena pause/lifecycle callbacks; existing Survival callers retain their original behavior. The prefab has editable flash, spark and smoke particle systems, a reused quiet impact clip, and an AudioSource at **0.08 volume**. Edit the prefab AudioSource to adjust volume/pitch or replace its clip. Edit the shared burst component for lifetime and the scene view for offset/scale. The final result panel waits 0.55 seconds by default so it does not cover the initial explosion; the win condition still resolves immediately.

Gym windows are inset into taller back walls, and the scoreboard faces inward on its own wall. **Room Details / Gym Wall Mounts** exposes the heights, insets and facing. The scoreboard's display is attached to the object rather than a floating area label. Stage equipment is restricted to the wings, and auditorium partition/storage groups use service areas, preserving the main stage and audience routes.

`ArenaFeedbackChecks.Run` checks ordinary event scheduling, Inspector links, hiding indicators without disabling events, actual expiry particles/audio, final-round presentation, pause and effect cleanup. Previews are saved under `Logs/ArenaFeedbackPreviews` in the validation project.

The gym wall prop is a proper decorative scoreboard: a small **GYM** heading, **HOME/AWAY** labels, two large score windows and a period readout. **Room Details / Decorative Scoreboard Readout** exposes labels, scores, period, row heights, spacing, sizes and colors. Text follows the angled board face; the larger frame and inset number windows are editable in the **Gym Scoreboard** prefab. It remains a school sports prop and does not introduce team scoring into Free For All.

## Arena motion and explosion polish

**Arena Player / Arena Participant** exposes visible movement speed, acceleration, input deadzone, contact-search precision, slide iterations and a small contact skin. A short acceleration ramp smooths starts and direction changes; releasing ordinary movement stops immediately. Slippery Floor can still deliberately add inertia. The existing walkability/player footprint checks remain authoritative.

`ArenaMovementCollision` sweeps small checked steps to contact, then projects remaining movement along the actual wall/player tangent. Normals are transformed correctly through the isometric projection, so sliding follows diagonal walls without gaining extra screen speed. Facing follows actual displacement rather than blocked input.

`ArenaLocomotionView` copies the existing character's sprite, color, flip, material and drawing order into a visual-only child. It adds a subtle stride, lean and settle; the logical body and colored ground ring stay planted. Health, identity, skins and HUD portraits still use the original player data. These skins have front/back poses, so this is procedural stride feedback rather than additional hand-drawn walking frames. Stride rate, height, sway, stretch and settle speed are editable on the arena prefab. Pause freezes the pose.

`ArenaExplosionAnimation` gives the potato burst a soft flash and a round ground shockwave, followed by sparks and fading smoke. Its saved flash/ring references, colors, duration, size curves, ring perspective and line width are editable on **Potato Explosion**. The native particle systems remain editable too. **Potato Burst.mat / Edge Softness** controls particle falloff through the arena-only soft-particle shader, avoiding a UI-button outline or an atlas image in the effect. Audio remains at 0.08 by default. The shared burst component still owns pause/lifetime cleanup.

`ArenaMotionChecks.Run` verifies keyboard input, equal diagonal speed, immediate release, generated-wall sliding, collision stress, pause, visual-only stride, explosion phases and the active graphics shader. These changes are limited to the real-time arena player; Race and Survival movement are unchanged.
