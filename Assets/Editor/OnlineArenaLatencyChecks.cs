using System;
using System.Collections.Generic;
using Escape4Now.Arena;
using Escape4Now.Multiplayer;
using Escape4Now.Multiplayer.RelayTesting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Simulates a guest waiting for updates without needing a Relay account or another computer.
public static class OnlineArenaLatencyChecks
{
    [Serializable] private sealed class SnapshotInfo { public ActorInfo[] actors; public int[] blockedCells; }
    [Serializable] private sealed class ActorInfo { public int movementRevision; }
    [Serializable] private sealed class InputInfo { public Vector2 move; }

    public sealed class Connection : IMatchMessageConnection
    {
        private readonly Queue<string> incoming = new Queue<string>();
        public bool Closed { get; private set; }
        public LanMessage LastCommand { get; private set; }
        public void Receive(LanMessage message) => incoming.Enqueue(LanMessageCodec.Serialize(message));
        public void Send(string json)
        {
            var message = JsonUtility.FromJson<LanMessage>(json);
            if (message.kind == "command") LastCommand = message;
        }
        public void Pump(Action<string> onMessage)
        {
            for (int i = 0; i < 32 && !Closed && incoming.Count > 0; i++) onMessage(incoming.Dequeue());
        }
        public void Dispose() { Closed = true; incoming.Clear(); }
    }

    public static void Check(OnlineArenaMatch adapter, string snapshot, Connection connection)
    {
        var arena = Object.FindFirstObjectByType<ArenaMatch>();
        var view = adapter.GetComponent<OnlineArenaPresentation>();
        Require(view != null, "The saved online scene exposes guest movement settings");
        adapter.enabled = false;
        view.Configure(arena, 1);
        Require(adapter.Apply(arena.Players.Count, snapshot), "Initialize a guest with the host's snapshot");
        var player = arena.Players[1];
        int revision = JsonUtility.FromJson<SnapshotInfo>(snapshot).actors[1].movementRevision;
        Vector2 start = player.Position;
        var initialMovement = player.CaptureMovement();
        int health = player.Character.Health;
        Vector2 direction = FindOpenDirection(arena, player);

        // No host updates arrive during these twelve frames (200 ms at 60 fps).
        for (int i = 0; i < 12; i++) view.Advance(direction, 1f / 60f);
        Vector2 predicted = player.Position;
        Require(Vector2.Distance(start, predicted) > .01f, "Guest movement responds before the next host reply");
        Require(player.Character.Health == health, "Predicting movement does not change health");

        Require(adapter.Apply(arena.Players.Count, snapshot), "Apply a delayed host update");
        Require(Vector2.Distance(predicted, player.Position) < .001f, "Pending movement is replayed after a correction");
        Require(adapter.Apply(arena.Players.Count, snapshot), "Apply a repeated update");
        Require(Vector2.Distance(predicted, player.Position) < .001f, "Repeated updates do not double the predicted distance");

        var predictedMovement = player.CaptureMovement();
        player.RestoreMovement(initialMovement);
        for (int i = 0; i < 6; i++) player.SimulateMovement(direction, 1f / 60f);
        var acknowledgedMovement = player.CaptureMovement();
        player.RestoreMovement(predictedMovement);
        view.BeginSnapshot();
        view.AcceptSnapshot(1, acknowledgedMovement, Vector2.zero, 6, 0, revision, false, 0);
        Require(view.PendingFrames == 6 && Vector2.Distance(predicted, player.Position) < .001f,
            "A partial acknowledgement replays only the remaining movement");

        // The host kept moving the guest for a few frames after input 6 arrived. Those frames must not be replayed twice.
        for (int moved = 3; moved <= 4; moved++)
        {
            player.RestoreMovement(initialMovement);
            for (int i = 0; i < 6 + moved; i++) player.SimulateMovement(direction, 1f / 60f);
            var hostMovement = player.CaptureMovement();
            player.RestoreMovement(predictedMovement);
            view.BeginSnapshot();
            view.AcceptSnapshot(1, hostMovement, Vector2.zero, 6, moved / 60f, revision, false, 0);
            Require(view.PendingFrames == 6 && Vector2.Distance(predicted, player.Position) < .001f,
                "Movement the host already made after input arrived is not replayed again (" + moved + " frames)");
        }

        view.BeginSnapshot();
        view.AcceptSnapshot(1, player.CaptureMovement(), player.ScreenVelocity, view.InputSequence, 0, revision, false, 0);
        Require(view.PendingFrames == 0, "The host's input acknowledgement clears old movement");

        for (int i = 0; i < 240; i++) view.Advance(direction, 1f / 60f);
        Vector2 capped = player.Position;
        for (int i = 0; i < 120; i++) view.Advance(direction, 1f / 60f);
        Require(view.PendingFrames <= 120 && player.Position == capped, "A stalled connection cannot create an unbounded prediction");
        Require(arena.Map.Layout.Fits(player.Position, arena.Map.Settings.playerRadius), "Prediction respects the normal wall clearance");

        view.BeginSnapshot();
        view.AcceptSnapshot(1, new ArenaParticipant.MovementState { position = start }, Vector2.zero, 0, 0, revision + 1, false, 0);
        Require(view.PendingFrames == 0 && player.Position == start, "A teleport or new round clears the old movement history");

        string paused = snapshot.Replace("\"paused\":false", "\"paused\":true");
        Require(adapter.Apply(arena.Players.Count, paused), "Apply host pause");
        Vector2 frozen = player.Position;
        view.Advance(direction, .2f);
        Require(player.Position == frozen && view.PendingFrames == 0, "Paused guests stay still");
        Require(adapter.Apply(arena.Players.Count, snapshot), "Resume from a host update");
        CheckBlockers(adapter, arena, snapshot);
        CheckImmediateInput(adapter, arena, connection);

        if (arena is HotPotatoController potato)
        {
            float seconds = potato.RemainingSeconds;
            var holder = potato.Holder;
            potato.AdvanceOnlineClock(.1f);
            Require(potato.RemainingSeconds < seconds, "The guest potato clock moves between packets");
            potato.AdvanceOnlineClock(1000);
            Require(potato.Playing && potato.Holder == holder && holder.Alive,
                "The guest clock never decides an explosion or elimination");
            Require(adapter.Apply(arena.Players.Count, snapshot), "Restore the host's clock");
        }
        Debug.Log("ONLINE ARENA LATENCY CHECKS PASSED: " + arena.Title);
    }

    private static void CheckBlockers(OnlineArenaMatch adapter, ArenaMatch arena, string snapshot)
    {
        var layout = arena.Map.Layout;
        for (int cell = 0; cell < layout.Width * layout.Height; cell++)
        {
            var point = new Vector2Int(cell % layout.Width, cell / layout.Width);
            if (!layout.Walkable(point) || !arena.CanOccupy(arena.Players[1], point)) continue;
            if (arena.Players.Exists(player => Vector2.Distance(player.Position, point) < 1.5f)) continue;
            var blocks = new List<int>(JsonUtility.FromJson<SnapshotInfo>(snapshot).blockedCells) { cell };
            int start = snapshot.IndexOf('[', snapshot.IndexOf("\"blockedCells\":", StringComparison.Ordinal));
            int end = snapshot.IndexOf(']', start);
            string blocked = snapshot.Substring(0, start + 1) + string.Join(",", blocks) + snapshot.Substring(end);
            Require(adapter.Apply(arena.Players.Count, blocked) && layout.blocked[point.x, point.y], "Guest prediction receives the host's temporary blockers");
            Require(adapter.Apply(arena.Players.Count, snapshot) && !layout.blocked[point.x, point.y], "The host can reopen a temporary blocked route");
            return;
        }
        throw new InvalidOperationException("No open tile for the blocker replication check.");
    }

    private static void CheckImmediateInput(OnlineArenaMatch adapter, ArenaMatch arena, Connection connection)
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var previous = InputSystem.settings.backgroundBehavior;
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            adapter.GetComponent<ArenaLocalInput>().ConfigurePrimaryKeyboard(arena.GamepadActions, null);
            // Make the normal keepalive wait very long. A direction change must still leave immediately.
            var nextSend = typeof(OnlineArenaMatch).GetField("nextInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            nextSend.SetValue(adapter, Time.unscaledTime + 60);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            adapter.SendLocalInput(Object.FindFirstObjectByType<LanMultiplayerSession>());
            Require(connection.LastCommand != null && JsonUtility.FromJson<InputInfo>(connection.LastCommand.modeState).move != Vector2.zero,
                "A new direction bypasses the held-input send interval");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            adapter.SendLocalInput(Object.FindFirstObjectByType<LanMultiplayerSession>());
            Require(JsonUtility.FromJson<InputInfo>(connection.LastCommand.modeState).move == Vector2.zero,
                "Releasing movement is sent immediately");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previous;
        }
    }

    private static Vector2 FindOpenDirection(ArenaMatch arena, ArenaParticipant player)
    {
        foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
        {
            bool open = true;
            for (int step = 1; step <= 8; step++)
                open &= arena.CanOccupy(player, player.Position + arena.Map.GridDirection(direction) * (.05f * step));
            if (open) return direction;
        }
        throw new InvalidOperationException("No open direction at the generated guest spawn.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Online arena latency check failed: " + message);
        Debug.Log("PASS: " + message);
    }
}
