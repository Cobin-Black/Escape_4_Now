# Internet multiplayer

## Scope

**Online Mode** offers private Relay rooms for Race, Survival, King of the Hill, and Hot Potato. Each online scene uses the same gameplay components and settings assets as its local mode. The host runs the match, and each guest controls their own slot.

**RelayConnectionTest** is a smaller diagnostic scene. It sends real messages both ways through Relay and checks repeated round trips. Creating a code or starting a host is not a pass. Its pass result does not prove different-network or school-network compatibility.

Private rooms use Relay join codes. A dropped player ends the online session. The host and guests can create a fresh room from the online menu.

## What was already in the project

- Unity **6000.3.25f1**, Input System **1.20.0**, existing local player and turn systems.
- LAN uses UDP broadcast discovery and direct TCP connections on port 47777. School Wi-Fi may isolate devices or block broadcasts/direct connections. That is a possible cause, not a verified diagnosis of the previous failure.
- The project already has a cloud project ID. That alone does not confirm service availability or Dashboard permissions.
- No Relay or Netcode SDK was installed before this work.
- The existing Race player is not a NetworkBehaviour. It does not need to become one for this first host-authoritative command/snapshot approach.

## Packages

- `com.unity.services.multiplayer` **2.4.1** (unified SDK; its Relay API is used directly).
- `com.unity.netcode.gameobjects` **2.13.3**, compatible with Unity 6.3.
- Unity Transport **2.7.4**, Authentication **3.8.0**, and other required services resolve as dependencies.

The latest Netcode 3.x requires a newer editor and was not selected. No standalone/deprecated Relay package or second networking framework was added. See [Unity's Relay/Netcode guide](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo).

## Unity setup

1. Open Project Settings > Services and confirm this is the intended team's cloud project.
2. The game's existing account sign-in and Relay use the same Unity project and environment. The standalone Relay diagnostic has its own temporary authentication profile.
3. Online Race uses **development**, matching the account service on main. Set the same environment on the standalone Relay tester when testing the Online Race configuration.
4. Review usage/billing in Unity Dashboard before enabling paid usage. No service account, private API key, or password is needed in the game.
5. Let Package Manager resolve the manifest. The scene uses the Input System UI module for pointer/navigation input.

Online Mode requires the player's existing game account for hosting and joining. The room shows the account name and allows skin selection.

## Open or build

- In the main menu, choose **Online Mode** and select a mode from the same four cards shown under **Game Mode**.
- Main-menu keyboard/gamepad navigation uses references to the project's existing `UI/Navigate`, `UI/Submit`, and `UI/Cancel` Input Actions. Sidebar spacing and online text are editable on the Main Menu component.
- All four online rooms use the arcade locker-card layout with skin arrows on your card. Same-device King of the Hill and Hot Potato use the same locker style with per-player control selection. Online rooms keep their code, readiness, and sign-in controls.
- Protocol selection and report export are under **Connection Settings**. The modal blocks controls behind it; the shared UI Cancel action closes it or returns from the lobby to the main menu. Actual errors remain available there and in exported reports.
- Open `Assets/Scenes/RelayConnectionTest.unity` for diagnostics only.
- The online scenes are `OnlineRace`, `OnlineSurvival`, `OnlineFreeForAll`, and `OnlineHotPotato`.
- Inspector configuration: `Assets/Settings/Networking/Relay Test Settings.asset` and `Online Race Connection.asset`.
- Build the tester with **Tools > Escape 4 Now > Relay Test > Build Standalone Tester**.
- Build the online prototype with **Tools > Escape 4 Now > Relay Test > Build Online Race Prototype**.
- The build commands write under ignored `Logs/RelayTestPlayer` and `Logs/OnlineRacePlayer`. Share the **whole build folder**, not only the EXE.
- The online build follows the normal Start Screen and Main Menu flow. All four online scenes are enabled in Build Settings.
- Authoring commands recreate these new scenes. Do not run them over designer edits without saving/backing them up. Existing game scenes and the regular Build Settings list are not rewritten.

## Private room flow

1. All players launch the same build, select the same game mode, and choose the same connection protocol.
2. Host picks 2, 3, or 4 players and selects **Host Room**.
3. Guests enter the displayed Relay code and select **Join Room**. No IP entry or port forwarding is used.
4. Players can choose an available skin. In Survival, the host can select a player as the monster or leave it AI-controlled.
5. Each player marks **Ready**. The host can start when all slots are ready. In Race and Survival, the host can enable **Fill Bots** to let the existing local bots fill seats that friends leave open.
6. The host runs the existing mode's gameplay. Input Actions on each computer control that computer's assigned slot.

Online Race uses the same turn HUD, locker menu, custom-dice selector, and key battle presentation as local Race. The host confirms match actions. Online matches skip the local account-stat write.

## Connection-test procedure with teammates

First use the diagnostic tester on two computers with **different internet connections** (for example home Wi-Fi and a phone hotspot, or two teammates' homes). Two app instances on one PC are a useful technical check but cannot replace this test.

For each protocol:

1. Both select **DTLS**, then one hosts and the other joins.
2. Wait for both screens to show **PASS**. Leave it connected for at least a minute for the manual stability check.
3. Enter a network label and select **Save Test Report on both computers before leaving**.
4. Stop/leave. Create a **fresh room** and repeat using **WSS**, then **UDP**.
5. Repeat with host/guest roles reversed.
6. Later repeat with a computer on school Wi-Fi and the other on an independent internet connection. Record whether school Wi-Fi requires a browser login/captive portal first.

UDP is an unencrypted diagnostic comparison. Prefer a working encrypted protocol for gameplay. WSS is not a firewall bypass: it still needs permitted TCP access to the endpoint supplied by Relay. [Unity's documented ports](https://docs.unity.com/en-us/mps-sdk/networking/ports) include API HTTP(S) 80/443, Relay endpoints in 37000–37100, and QoS UDP 7778. Check the actual reported endpoint rather than assuming WSS always means port 443. Do not turn off a school's security controls; ask IT about an allowlist if required.

## If a connection fails

The screen and report preserve the exception type, service error code when available, failed stage, selected protocol, and public Relay endpoint. Authentication credentials, connection payloads, and Relay keys are not intentionally written to these reports. Keep the associated Unity Player.log too; transport warnings can provide more detail than the disconnect callback.

| Failed stage / observation | What to investigate |
|---|---|
| Services initialization / anonymous authentication | Cloud-project linkage, existing environment, service permissions/configuration, internet/DNS/TLS access, captive portal, Unity service availability |
| Relay allocation / join code | Relay availability for project, code correctness/expiry, capacity, region, quotas, HTTP/service error |
| Transport connection after successful allocation | Host still running, matching versions/protocol, actual Relay endpoint reachable, firewall/proxy restrictions, transport configuration |
| Peer connected, but two-way replies time out | Message/transport errors in Player.log, connection loss, congestion, application loop problems |
| DTLS/UDP fail but WSS passes in the same controlled test | Evidence consistent with UDP restrictions; not proof by itself |
| Home test passes and otherwise identical school test fails | Evidence consistent with a school-network restriction; compare both logs and endpoint access with IT |
| No guest joined before host waiting timeout | No remote participant was observed; this alone does not diagnose a network failure |

Do not label a timeout as a firewall problem without comparison evidence. The app cannot independently verify which internet networks the testers are using.

Reports are stored in `Application.persistentDataPath/RelayTests`; the UI shows the full path after saving. A report marked **Pass=false** after a later disconnection is intentional: an earlier exchange does not prove the connection remained healthy.

## Playable Race verification after connectivity

Test 2, then 3, then 4 computers. Compare the generated rooms/obstacles/events/exit, player identities, starting order, dice, current-player HUD, timer, movement, inventory pickups/use, key battles, Pause/resume, and results on every screen. Try invalid requests, wrong/full/closed codes, late joining, guest disconnect, host disconnect, leaving, and creating another room. Keep testing local modes separately.

The host's seed plus serialized map/furniture configuration recreates the base map. Explicit authoritative object and player snapshots carry details that should not be independently randomized by clients. Client spawners are already suppressed by the existing network-view path.

## Code responsibilities

- `RelayRoomConnection`: allocation/join, transport, timeouts, cancellation, connection approval, round-trip health checks.
- `RelayTestIdentity`: initialization and anonymous authentication.
- `RelayTestReport`: bounded, credential-filtered diagnostics/export.
- `RelayTestView`: diagnostic UI bindings.
- `OnlineRaceSession` / `OnlineRaceLobbyView`: shared online lobby, player settings, readiness, and transport setup.
- `OnlineSurvivalMatch`: host-owned Survival turns, monster input, outcomes, puzzles, and outdoor effects.
- `OnlineArenaMatch`: shared arena input, map and event updates, with host acknowledgements for movement.
- `OnlineArenaPresentation`: guest movement prediction, replay of inputs still in flight, small visual corrections, and bounded movement between updates for other players. The host supplies health, items, passing and results.
- `OnlineKingOfTheHillMatch`: hill position, scores, respawns, pickups and item effects.
- `OnlineHotPotatoMatch`: round, holder, timer and explosion.
- `RaceNetworkActions`: Race Input Actions and host checks for turns, storage, dice, items and key battles. LAN and Relay both use it.
- `OnlineLobbyAuthoring`: saved Canvas layout and existing main-menu font/palette reuse. Layout, colors, fonts, references, and labels can be edited on the saved scene objects; the authoring command recreates the lobby UI, so save designer edits before running it.
- `RelayMessageChannel`: reliable fragmented Netcode messages adapted to the existing match-message boundary.
- `IMatchMessageConnection`: shared boundary used by both LAN sockets and Relay channels.
- Existing `LanMultiplayerSession`, `LanMessageCodec`, and `LanWorldView`: shared match connection, message format, and Race board updates. Their historical LAN names remain to avoid a broad rename of working systems.
- `RelayTestAuthoring` / `RelayFoundationChecks`: saved scene/build creation and structural checks.
- Development-only command-line components automate local-process checks; they do not prove different-network access.

## Edit multiplayer in Unity

- **Connection**. Open `Assets/Settings/Networking/Online Race Connection.asset` for shared Relay settings. Select the match session in an online scene for update intervals and timeouts.
- **Controls**. Open `Assets/Settings/InputSystem_Actions.inputactions` for Race, Survival, menus, and the Player map used by controllers and the first keyboard player. King of the Hill uses Attack. Hot Potato uses Potato Pass. Edit `Assets/Settings/Arena/Arena Controls.inputactions` for additional local keyboard layouts. Online arena matches use the Player actions on each computer. Their adapters expose gamepad preference and movement update rate, and follow controller changes.
- **Player colors**. Edit `Assets/Settings/Player Colors.asset` to change Player 1 through Player 4 across every local and online mode. Locker accents, player indicators and battle dice follow those colors.
- **Battle dice**. The Race Battle UI component references the existing HUD Dice Display. Both battle dice copy that saved frame and its face sprites. Their size, spacing, hop and animation timings are editable under Battle Dice.
- **Mode rules**. Select the mode controller in the scene to edit the same gameplay settings used by the local version. Each online adapter sends the state needed by that mode. Shared room controls are on the room and lobby components.

## Guest responsiveness

Online scenes publish match state every 0.05 seconds, after the current frame has run its gameplay updates. Direction changes and movement release are sent immediately. Held input keeps its normal configurable send rate.

King of the Hill and Hot Potato guests move their own character using the shared arena motor while waiting for the host. Each update acknowledges the input the host has received. The guest restores the host movement state and replays its remaining input. Spawns, teleports, knockback, pauses and eliminations reset the relevant prediction. The host also sends current blocked cells so prediction respects moved equipment and temporary obstacles.

Select the Online Arena Presentation component in either online arena scene to edit prediction, maximum pending movement, correction speed, snap distance and the limit for projecting other players forward. Very slow connections reach the configured prediction limit and wait for an update.

Hot Potato guests animate the remaining time between updates and estimate transit time from measured ping. The host confirms the holder, explosions and eliminations. Network travel time still affects when confirmed events arrive.

The shared Online Race Connection asset now uses connection version 22. All participants need the updated build, which includes movement and lobby profile acknowledgements. The existing version check rejects earlier builds before they enter the room.

Online rooms publish the completed account login and later display-name changes. Guest profile requests are retried until the host acknowledges the newest revision, so an early request cannot be lost during slot assignment. Repeated requests do not reset Ready. A changed name or skin needs Ready again. The retry interval is editable on Online Race Session in the Inspector; all four modes share this room code.

Joining a room for another game mode shows which mode to select. Rejected guests do not close the host's room. Failed lobby connections clear both the Relay and gameplay sessions, allowing another host/join attempt after transport shutdown completes. Ordinary game rooms keep waiting for friends; **Limit Host Waiting** in the connection settings can enable a timeout when needed. The diagnostic scene retains its timed test. These recovery paths are checked with simulated approval/disconnect callbacks and real local transport restarts; cross-network recovery still needs a teammate test.

## Free-tier / costs

Unity currently lists Authentication as free and Relay's first **50 average monthly concurrent users** as free, with **3 GiB per CCU up to 150 GiB/month** included bandwidth. Usage beyond free allowances can be charged under pay-as-you-go. These are service allowances, not an unlimited-free promise. Check [current Unity pricing](https://unity.com/products/gaming-services/pricing) and the team's Dashboard usage/billing controls. This work does not enroll the account in a paid plan or add payment details.

## Test status

- Guest response update on 2026-11-18 passed Unity checks for a simulated 200 ms gap between updates, partial input acknowledgements, repeated snapshots, prediction limits, pause/teleport resets, temporary blockers, immediate movement/release packets, and the guest potato clock. Both arena modes, the local arena controls, Race HUD, all online room scenes and saved connection references passed. Live Relay testing of this update with teammates is pending.
- Presentation update on 2026-11-17 passed Unity Play Mode checks for all four online rooms, local and online Race battle dice, local arena controls, and Survival HUD/input. The shared palette is assigned in all eight mode scenes. Live Relay testing of this update with teammates is pending.
- Native Windows builds and saved-reference/Input System checks: **passed**.
- Existing LAN socket framing, queue limits, disconnect, and discovery regressions: **passed**.
- Real Relay, two standalone processes on this one home-network computer: **DTLS passed; WSS passed; UDP passed**, on both host and client. Each protocol exchanged at least ten round trips during the observation window. Reports are captured before deliberate test-process teardown.
- Online Race, two standalone processes through Relay: **passed**. Both observed the same map seed and at least three turn transitions; each observed its own authoritative roll and actual grid movement. This is a first-playable smoke check, not exhaustive gameplay validation.
- Updated Windows build, two standalone processes through Relay on one computer: **passed** in all four modes. The runs covered Race rolls, Survival turns, arena movement, player-monster input, and Race/Survival rooms with an open slot filled by a bot.
- Native test evidence is under ignored `Logs/RelayMatrix`, `Logs/online-race-final-*-result.txt`, and `Logs/relay-release-check.log`.
- Different internet networks: Online Race was verified by two teammates on separate Wi-Fi networks before this update.
- School Wi-Fi: **not tested** — current computer is on home internet.
- Cross-network coverage for the updated build and the three added online modes still needs a teammate test.
