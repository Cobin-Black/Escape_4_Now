# Events

This page describes Race/indoor events. Survival reuses the shared event state and effect helpers but has its own placement and outdoor pool; see [Survival Mode Rules](SurvivalMode.md).

Events activate during normal movement, including when passing through a tile.
Forced movement and teleports never activate another event.
Every event has a prefab in `Assets/Prefabs/Event Tiles/Resources/EventTiles`.
The Latest Result panel names the affected player and explains what happened.
It stays visible until the next result. Blackout also shows its remaining turns.
Event tiles are still visible; hidden events and setup counts are not added yet.

## Positive

- Lucky Roll: use the higher of two dice on the next turn.
- Extra Step: add up to two moves immediately.
- Second Chance: block one negative event, then remove the protection.

## Negative

- Freeze: skip the next turn. Multiple Freeze effects still skip only one turn.
- Bad Roll: use the lower of two dice on the next turn.
- Stumble: lose up to two remaining moves, never going below zero.

## Neutral

- Warp: teleport to a safe empty tile and keep unused moves.
- Random Event: receive one effect from the list below.
- Wrong Room: teleport to a safe tile in another main room.

Random Event has a 20% chance each for forward two spaces, backward two spaces,
Lucky Roll, or Bad Roll. Freeze and Warp each have a 10% chance.
Second Chance blocks backward movement, Bad Roll, or Freeze from this event.
Forced movement stops before walls or other players.

## Global

- Blackout: reduce everyone's visibility immediately. Lasts four full player
  turns with two players, three with three players, or two with four players.
  The starting partial turn does not count. Another Blackout cannot extend it.
- Location Switch: give everyone another player's old position.
  If shared positions make it impossible to move everyone, nobody moves.
- Fire Drill: move everyone to different clear hallway tiles.
- Good Luck Day: add one move to each player's next normal dice roll, once.

Global events ignore both Second Chance and protection from other players.
Teleports and switches keep inventories and remaining moves unchanged.
If there are not enough safe destinations, nobody is moved by Fire Drill.

## Placement and Saved Effects

Each type can appear once per map. Small events always appear when space allows.
Warp and Wrong Room have a 60% appearance chance. Freeze, Blackout,
Location Switch, and Fire Drill have a 35% chance. Not every match has every type.
Tiles avoid players, obstacles, exits, other events, and reserved special-room items.

Lucky Roll and Bad Roll replace each other: the newest effect wins.
Second Chance and Good Luck Day do not stack. Their bonuses wait until used.
The host handles effects during network play. All players need the same build.
Item-specific dice rules are unchanged.
