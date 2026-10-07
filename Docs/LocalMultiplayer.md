# Local Multiplayer

2 to 4 people play on the same computer and take turns using one keyboard.
There is no Wi-Fi or online play.

## Start a Game

1. Open `SampleScene` and press Play.
2. Pick 2, 3, or 4 players, then press **Start Match**.
3. Press Space to roll and use WASD or the arrow keys to move.
4. Use **End Turn** to pass after rolling and finishing a move.
5. **Leave Match** goes back to setup.

The colored markers match the players on the map. Only the current player can move.

## Replace the Setup UI

This UI is temporary. The team's final UI will replace it.
Use these methods on `LocalMultiplayerSession`:

- `SetTemporaryUIVisible(false)` hides this setup UI.
- `SetPlayerCount(count)` picks the number of players.
- `StartLocalMatch()` starts the game.
- `ReturnToSetup()` goes back to setup.
- `MatchStarted` and `Status` show whether the game started or something went wrong.

If the final menu is in another scene, pass the player count to the game scene first.

## Test

Try 2, 3, and 4 players. Check rolling, movement, turn order, and leaving the match.
The automated Play Mode checks are in `Assets/Editor/LocalMultiplayerChecks.cs`.
