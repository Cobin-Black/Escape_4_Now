# Internet multiplayer — first playable foundation

## Scope

The first slice is a separate **OnlineRace** scene with private Relay rooms. It reuses the existing authoritative Race gameplay. Local Race, Survival, Free For All, Hot Potato, and the old LAN entry points remain separate.

**RelayConnectionTest** is a smaller diagnostic scene. It sends real messages both ways through Relay and checks repeated round trips. Creating a code or starting a host is not a pass. Its pass result does not prove different-network or school-network compatibility.

There is no Quick Play, Sessions lobby, account registration, friends system, host migration, reconnect, or online conversion of the other three modes in this slice. These remain later stages. Relay allocation codes used here are not interchangeable with future Sessions/Lobby codes.

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
2. A project owner/admin should verify that Authentication anonymous sign-in and Relay are available for the selected environment. Both computers must use the same project and environment.
3. Online Race uses **development**, matching the account service on main. Set the same environment on the standalone Relay tester when testing the Online Race configuration.
4. Review usage/billing in Unity Dashboard before enabling paid usage. No service account, private API key, or password is needed in the game.
5. Let Package Manager resolve the manifest. The scene uses the Input System UI module for pointer/navigation input.

Online Race uses the signed-in game account when available. Otherwise it signs in with a temporary Relay profile and restores the previous profile when the scene closes.

## Open or build

- Main menu: **Online Mode** (above **Game Mode**) > **Play Online** opens the private Online Race lobby. It keeps the existing arcade menu design; the other online modes are listed as not available yet.
- Main-menu keyboard/gamepad navigation uses references to the project's existing `UI/Navigate`, `UI/Submit`, and `UI/Cancel` Input Actions. Sidebar spacing and online text are editable on the Main Menu component.
- The online lobby now uses the same arcade palette and fonts, with separate Create/Join and connected-room views. Player portraits, Host/You labels, ready badges, and the room code replace the old diagnostic-heavy layout. It fits narrower windows too.
- Protocol selection and report export are under **Connection Settings**. The modal blocks controls behind it; the shared UI Cancel action closes it or returns from the lobby to the main menu. Actual errors remain available there and in exported reports.
- Open `Assets/Scenes/RelayConnectionTest.unity` for diagnostics only.
- Open `Assets/Scenes/OnlineRace.unity` for private online Race.
- Inspector configuration: `Assets/Settings/Networking/Relay Test Settings.asset` and `Online Race Connection.asset`.
- Build the tester with **Tools > Escape 4 Now > Relay Test > Build Standalone Tester**.
- Build the online prototype with **Tools > Escape 4 Now > Relay Test > Build Online Race Prototype**.
- The build commands write under ignored `Logs/RelayTestPlayer` and `Logs/OnlineRacePlayer`. Share the **whole build folder**, not only the EXE.
- The online prototype build now follows the normal Start Screen > Main Menu flow. OnlineRace is enabled in Build Settings without changing the order of the existing scenes.
- Authoring commands recreate these new scenes. Do not run them over designer edits without saving/backing them up. Existing game scenes and the regular Build Settings list are not rewritten.

## Private online Race flow

1. All players launch the same OnlineRace build and choose the same protocol.
2. Host picks 2, 3, or 4 players and selects **Host Room**.
3. Guests enter the displayed Relay code and select **Join Room**. No IP entry or port forwarding is used.
4. Each player marks **Ready**. The host can start only when every selected slot is connected and ready.
5. The host runs Race's normal map generation, randomized repeating turn order, dice, movement, events, items, timer, and win rules. Clients send requests; the host sends results.
6. Existing Input Actions control the locally owned slot. A guest cannot pick another player's slot in a command.
7. Late joins are rejected after start. A lost peer stops the prototype; leave and create another room. Host migration/reconnect are not implemented.

This first lobby uses the existing default numbered identities, colors, and skin assignments on both computers. Remote name/skin selection is a later lobby feature. The normal local setup menu is disabled only in the OnlineRace scene; the existing match HUD, Pause, inventory controls, and result UI are reused.

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
- `OnlineRaceSession` / `OnlineRaceLobbyView`: Race online lobby, ready state, transport adapter wiring, presentation.
- `OnlineLobbyAuthoring`: saved Canvas layout and existing main-menu font/palette reuse. Layout, colors, fonts, references, and labels can be edited on the saved scene objects; the authoring command recreates the lobby UI, so save designer edits before running it.
- `RelayMessageChannel`: reliable fragmented Netcode messages adapted to the existing match-message boundary.
- `IMatchMessageConnection`: shared boundary used by both LAN sockets and Relay channels.
- Existing `LanMultiplayerSession`, `LanMessageCodec`, and `LanWorldView`: reused Race authority, command validation, snapshots, and client display. Their historical LAN names remain to avoid a broad rename of working systems.
- `RelayTestAuthoring` / `RelayFoundationChecks`: saved scene/build creation and structural checks.
- Development-only command-line components automate local-process checks; they do not prove different-network access.

Future Sessions/Quick Play should filter mode and content/build version, lock started rooms, clean up cancelled operations, and keep readiness/identity separate from game rules. Survival, arena movement, and Hot Potato timers/ownership must get their own authoritative synchronization adapters before being offered online.

## Free-tier / costs

Unity currently lists Authentication as free and Relay's first **50 average monthly concurrent users** as free, with **3 GiB per CCU up to 150 GiB/month** included bandwidth. Usage beyond free allowances can be charged under pay-as-you-go. These are service allowances, not an unlimited-free promise. Check [current Unity pricing](https://unity.com/products/gaming-services/pricing) and the team's Dashboard usage/billing controls. This work does not enroll the account in a paid plan or add payment details.

## Test status

- Native Windows builds and saved-reference/Input System checks: **passed**.
- Existing LAN socket framing, queue limits, disconnect, and discovery regressions: **passed**.
- Real Relay, two standalone processes on this one home-network computer: **DTLS passed; WSS passed; UDP passed**, on both host and client. Each protocol exchanged at least ten round trips during the observation window. Reports are captured before deliberate test-process teardown.
- Online Race, two standalone processes through Relay: **passed**. Both observed the same map seed and at least three turn transitions; each observed its own authoritative roll and actual grid movement. This is a first-playable smoke check, not exhaustive gameplay validation.
- Native test evidence is under ignored `Logs/RelayMatrix`, `Logs/online-race-final-*-result.txt`, and `Logs/relay-release-check.log`.
- Different internet networks: **not tested** — no second computer available during implementation.
- School Wi-Fi: **not tested** — current computer is on home internet.
- Full online multiplayer across all four modes: **not implemented** in this first slice.
