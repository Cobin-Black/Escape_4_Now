using System;
using System.IO;
using System.Threading.Tasks;
using Escape4Now.Arena;
using Escape4Now.GameModes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Development builds can prove that a second process receives a running match and can act.
    internal static class OnlineModeSmokeCheck
    {
#if DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async void Run()
        {
            string scene = Argument("-onlineModeCheck");
            if (string.IsNullOrEmpty(scene)) return;
            bool host = Argument("-onlineRole") == "host";
            string output = Argument("-onlineResult");
            try
            {
                await Escape4Now.Accounts.AccountService.ConnectAsync();
                if (!Escape4Now.Accounts.AccountService.IsSignedIn)
                    throw new InvalidOperationException("Sign in with a game account on this test installation before running the online match check.");
                SceneManager.LoadScene(scene);
                await Task.Delay(500);
                var room = UnityEngine.Object.FindFirstObjectByType<OnlineRaceSession>();
                var transport = UnityEngine.Object.FindFirstObjectByType<LanMultiplayerSession>();
                if (host)
                {
                    bool withBot = Argument("-onlineBot") == "true";
                    await room.HostAsync(withBot ? 3 : 2);
                    if (room.Relay.Active)
                    {
                        if (withBot) room.ToggleFillWithBots();
                        if (Argument("-onlineMonster") == "guest")
                        {
                            room.CycleMonsterRole();
                            room.CycleMonsterRole();
                        }
                        File.WriteAllText(Argument("-onlineCodeFile"), room.Relay.RoomCode);
                    }
                }
                else await room.JoinAsync(Argument("-onlineCode"));

                float deadline = Time.realtimeSinceStartup + 50;
                bool moved = false;
                bool readySent = false;
                Vector2? start = null;
                bool rolled = false;
                while (room.Relay.Active && Time.realtimeSinceStartup < deadline)
                {
                    if (!room.Playing && !readySent && room.LocalSlot >= 0 && room.PlayerCount == 2)
                    {
                        room.ToggleReady();
                        readySent = true;
                    }
                    if (room.CanStart) room.StartMatch();
                    if (room.Playing && room.LocalSlot >= 0)
                    {
                        var arena = UnityEngine.Object.FindFirstObjectByType<ArenaMatch>();
                        if (arena != null)
                        {
                            Vector2 position = arena.Players[room.LocalSlot].Position;
                            start ??= position;
                            moved |= Vector2.Distance(position, start.Value) > .08f;
                            if (!moved)
                                transport.SubmitModeInput("{\"move\":{\"x\":0.4,\"y\":0.5}}");
                        }
                        else if (scene == "OnlineRace")
                        {
                            if (transport.IsMyTurn && transport.LatestState != null)
                            {
                                var player = transport.LatestState.players[room.LocalSlot];
                                if (!player.rolled) transport.Submit("roll");
                                else
                                {
                                    moved = player.roll > 0 && player.roll == player.firstDie + player.secondDie;
                                    if (moved && !player.rolling) transport.Submit("end");
                                    if (player.rolling) moved = false;
                                }
                            }
                        }
                        else
                        {
                            var mode = UnityEngine.Object.FindFirstObjectByType<SurvivalModeController>();
                            var local = UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>();
                            if (room.MonsterSlot == room.LocalSlot)
                            {
                                if (transport.IsMyTurn && !mode.Monster.HasRolled) transport.Submit("roll");
                                moved = mode.Monster.HasRolled && mode.Monster.LastRoll > 0;
                                if (moved && room.Relay.Passed) break;
                                await Task.Delay(110);
                                continue;
                            }
                            int survivor = room.MonsterSlot >= 0 && room.LocalSlot > room.MonsterSlot ? room.LocalSlot - 1 : room.LocalSlot;
                            var player = local.Participants[survivor];
                            if (transport.IsMyTurn && !player.HasRolled)
                                transport.Submit("roll");
                            rolled |= player.LastRoll > 0;
                            if (transport.IsMyTurn && player.HasRolled)
                            {
                                foreach (var step in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                                {
                                    var cell = player.GridPosition + step;
                                    if (!mode.GridMap.IsWalkable(cell)) continue;
                                    transport.SubmitModeInput("{\"action\":\"move\",\"round\":" + mode.Turns.TurnNumber
                                        + ",\"a\":" + cell.x + ",\"b\":" + cell.y + "}");
                                    break;
                                }
                            }
                            moved = rolled;
                        }
                        if (moved && room.Relay.Passed) break;
                    }
                    await Task.Delay(110);
                }
                if (!room.Playing || !moved || !room.Relay.Passed)
                    throw new InvalidOperationException("Match=" + room.Playing + " Action=" + moved
                        + " Room=" + room.Status + " MatchStatus=" + transport.Status + " Relay=" + room.Relay.Error);
                File.WriteAllText(output, "PASS " + scene + (host ? " host" : " guest"));
                await Task.Delay(12000);
                Application.Quit();
            }
            catch (Exception error)
            {
                File.WriteAllText(output, error.ToString());
                Application.Quit(2);
            }
        }

        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
#endif
    }
}
