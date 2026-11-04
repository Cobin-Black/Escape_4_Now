# Free For All and Hot Potato events

Events now activate on a real-time schedule. They are not collectible floor markers. Health packs, shields, boost pickups and other future items remain outside these pools.

Select **Mode Events** in either scene. Its Inspector shows the referenced pool inline under **Event Details / Add or Remove Events**, followed by the selected and active events during Play Mode.

## Event pools

Free For All supports Slippery Floor, Ball Barrage, Bleachers Move, Lights Out, Hazard Zone, Falling Equipment, Player Swap, Knockback Wave and Court Shuffle.

Hot Potato supports Spotlight, Curtain Drop, Blackout, Seat Block, Sound Blast, Player Swap, Potato Shuffle, Extra Time, Time Cut, Falling Prop and Moving Stage Lights.

Locker Room Opens and Stage Door Opens are saved as disabled entries. These rooms currently have open passages rather than functional shortcut doors. The runtime rejects these entries even if their checkbox is turned on; a future door implementation can provide a real hook. No temporary shortcut is simulated by deleting a wall.

## Unity settings

`FreeForAll Events.asset` and `HotPotato Events.asset` control each mode separately. Adjust the enabled list, selected type count, weights, initial delay, activation interval, per-event cooldown, maximum occurrences, duration, warning time and simultaneous event limit.

Each match selects a weighted subset without repeated types. Activation also uses weights. Player Swap, Potato Shuffle, Time Cut, Blackout and waves have lower starting weights. The selected subset stays with that match; ordinary rounds do not replace it. Active effects are cleared between potato rounds. Timers and warning animations stop during Pause.

The shared `Event Visuals.asset` exposes line width, warning colors, blackout opacity, fade speed, spotlight beams, ball bounce, ball size, materials and environmental prefab references. Each event can assign its own warning and activation sound. Audio levels are intentionally quiet.

Slippery Floor exposes steering response. Hazard Zone exposes damage, knockback and repeat-hit interval. Balls expose count, interval, speed and distance. Waves expose radius, push distance and expansion time. Moving Stage Lights expose their movement penalty; the effect only applies inside the visibly lit zone. Time Cut respects a configurable minimum remaining potato time after activation, and never silently cuts the clock straight to zero.

## Safety and interaction

All participants are human-controlled. Warnings and results are shared on the same screen. Dangerous areas and incoming ball directions are marked before activation. Swaps show both targets; potato transfer keeps the normal holder marker and adds a visible connection between the old and new holders. Neither swapping nor knockback changes identity, inventory or health ownership.

Temporary block placement checks current player footprints and the existing room connectivity. It checks again at activation and when a falling object lands. Walking into the warned landing space cancels an unsafe landing. Existing active blocks and avoidable hazard zones are considered together, so two effects cannot close the only safe route. Expiry, leaving setup, scene changes and match end remove temporary event blockers.

Court Shuffle moves an existing movable prop to a validated destination; it does not create replacement obstacles. Its new placement stays for the rest of that match. Bleachers expand only into a reserved, safe adjacent tile, then retract when their event ends. Shortcut doors stay disabled.

## Reuse and code ownership

- `ArenaEventDirector`: weighted selection, scheduling, warnings, cooldowns and lifetimes for both modes.
- `ArenaEnvironmentalEvents`: real-time event behaviors, using existing movement, health and potato hooks.
- `ArenaEventSafety`: player-footprint checks and validation through the existing connected room data.
- `ArenaEventView` and `ArenaEventLightingUI`: shared presentation driven by event state.
- `SharedPositionEffects`: atomic destination validation and exchange, shared with the existing grid-player switch implementation. Race and Survival keep their original eligibility and occupancy checks.
- `SurvivalEnvironmentalPlacement.NaturalPatch`: reused to grow connected environmental regions.
- `SurvivalEnvironmentalSurface`: reused wet-ground renderer through a map-projection overload. Existing outdoor calls retain their old behavior.
- `SurvivalBranchFall`: reused falling animation with an optional arena Pause callback. Existing Survival calls retain their old Pause behavior.
- `UIBlackout.shader`: reused for uniform arena dimming; the arena does not give a private visibility circle to one participant.
- `ArenaParticipant.Knockback`, player health, `HotPotatoController` clock/holder functions and `ArenaLayout.Connected/Fits`: reused directly.

These automatic environmental events need no extra input action. Player movement, tagging and Pause continue to use the saved Input System actions.

## Compact room visuals

Both room settings default to **20×20**, with separated safe spawns and alternate routes. Size remains editable. The shared `Room Visuals.asset` references editable seats, lockers, windows, sports equipment, scoreboard, speakers, desks and curtain details. Decorative objects stay on existing blocked ground; the visual pass does not reserve additional walkable tiles.

Gym floor side faces no longer draw a repeated square grid. Court paint, wood colors and blue bleachers distinguish the gym; upholstered red chairs, stage trim and curtain pleats distinguish the auditorium. The HUD sizes itself to its visible content. The existing `MatchCanvasVisibility` component hides UI outside Play Mode unless Editor Preview is enabled.

## Validation

`ArenaEventChecks.Run` exercises both mode pools with 2, 3 and 4 players, including warning and duration Pause, swaps, clock changes, block cleanup, ball knockback, hazard damage, slippery steering, unsafe landing cancellation and seed reproduction. `ArenaChecks.Generation` validates seeded compact layouts and spawns. Tests run in the isolated validation copy, preserving the active project scene.
