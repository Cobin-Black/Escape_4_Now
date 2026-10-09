# Local Multiplayer

2 to 4 people play on the same computer and take turns using one keyboard.
Or 2 to 4 people can play on separate computers connected to the same Wi-Fi or router.

## Start a Game

1. Open `SampleScene` and press Play.
2. Pick 2, 3, or 4 players, then press **Start Match**.
3. Press Space to roll and use WASD or the arrow keys to move.
4. Use **End Turn** to pass after rolling and finishing a move.
5. **Leave Match** goes back to setup.

The colored markers match the players on the map. Only the current player can move.

## Play over Wi-Fi

Use the same game build on every computer. In setup, select **Wi-Fi**.

1. On one computer, choose the total player count and press **Host Match**.
2. Share the displayed six-digit room code with the other players.
3. On each other computer, enter that code and press **Join Match**. The game finds the host automatically.
4. The host presses **Start Match** when all selected players are connected.

Each computer controls one player. Space rolls, WASD or arrows move, and clicking a floor tile also moves.
The buttons at the bottom use items or take one from a nearby obstacle. Custom Dice lets you choose 1 to 6.
The host handles rolls, movement, events, pickups, and wins. Everyone receives the same results.
If someone disconnects during a match, leave and start a new match. There is no host migration or reconnect yet.

Allow the game through Windows Firewall on **Private networks** if prompted. The host uses TCP and UDP port 47777.
UDP finds the room; TCP carries the match. A search stops after six seconds if no matching room responds.
No router port forwarding is needed. Guest or school Wi-Fi may block connections between computers.
The room code limits who can join; this version is for a trusted local network and does not encrypt traffic.

## Replace the Setup UI

This UI is temporary. The team's final UI will replace it.
Use these methods on `LocalMultiplayerSession`:

- `SetTemporaryUIVisible(false)` hides this setup UI.
- `SetPlayerCount(count)` picks the number of players.
- `StartLocalMatch()` starts the game.
- `ReturnToSetup()` goes back to setup.
- `MatchStarted` and `Status` show whether the game started or something went wrong.

If the final menu is in another scene, pass the player count to the game scene first.

For Wi-Fi, call `StartHost(count)`, `Join(code)`, or `StartHostedMatch()` on `LanMultiplayerSession`.
The older `Join(ip, code)` method remains available for tests, but the menu does not ask for an IP.
`Status`, `RoomCode`, and `ConnectedPlayers` provide lobby information. `ReturnToSetup()` closes the connection.

## Test

Players can walk through each other. Each crossed tile still costs one move.
An occupied destination stops the route at the last clear tile before it.
For an adjacent occupied tile, a direction input or click continues to the next
clear tile in that direction. It needs enough moves for every crossed tile and
cannot cross a wall. With too few moves, the player stays in place.
Crossed events still work. While passing through someone, the last clear tile
remains the moving player's safe position so players cannot finish on the same tile.
Location Switch uses those separate safe positions if it triggers during transit.
Forced movement does not activate events it crosses or lands on. This includes
Random Event movement, Warp, and Location Switch. Teleporting keeps held items.

Future stealing or direct negative actions must first call `victim.CanBeTargetedBy(attacker)`.
After the action succeeds, call `victim.RecordSuccessfulTargetedAction(attacker)`.
This protects the victim from further direct actions until their next turn begins.
Failed actions should not call it. Environmental events and Location Switch ignore this protection.
There are no stealing or direct attack actions yet; these methods prepare the shared rule.

Try 2, 3, and 4 players. Check rolling, movement, turn order, and leaving the match.
The automated Play Mode checks are in `Assets/Editor/LocalMultiplayerChecks.cs`.

The LAN transport test is `dotnet run --project Tests/LanChecks/LanChecks.proj`.
The Play Mode runner also accepts `-lanAuthority` to check invalid requests and disconnects.
`LanBuildChecks.Build` makes a development test build in `Logs/LanPlayer`.
Its `-lanCheck host` and `-lanCheck client -roomCode CODE` flags run a four-process smoke test.
These test flags are excluded from release builds. Try the final build on two real computers too.
