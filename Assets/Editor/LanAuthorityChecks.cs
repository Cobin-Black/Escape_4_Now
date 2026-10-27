// Checks that only the LAN host applies gameplay changes and that clients cannot act for other players.

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using UnityEngine;

//Tests actual network requests against the host's ownership and game-rule checks.
public static class LanAuthorityChecks
{
    public static IEnumerator Run()
    {
        yield return null;
        LanMultiplayerSession lan = LanMultiplayerSession.Instance;
        Require(lan.StartHost(2), "Host starts");
        //These command-forgery scenarios explicitly use host then guest; random-order checks run separately.
        var turnSettings = new UnityEditor.SerializedObject(UnityEngine.Object.FindFirstObjectByType<Escape4Now.TurnSystem.TurnSystemController>());
        turnSettings.FindProperty("randomizeStartingOrder").boolValue = false;
        turnSettings.ApplyModifiedPropertiesWithoutUndo();
        using (LanConnection wrong = Connect())
        {
            wrong.Send(LanMessageCodec.Serialize(new LanMessage { kind = "hello", code = "invalid" }));
            for (int i = 0; i < 15; i++) { wrong.Pump(_ => { }); yield return null; }
            Require(wrong.Closed && lan.ConnectedPlayers == 1, "Wrong room code is rejected");
        }
        using (LanConnection bad = Connect())
        {
            bad.Send("{ broken json");
            for (int i = 0; i < 15; i++) { bad.Pump(_ => { }); yield return null; }
            Require(bad.Closed && lan.ConnectedPlayers == 1, "Malformed peer cannot stop the host");
        }
        using (LanConnection guest = Connect())
        {
            string handshake = LanMessageCodec.Serialize(new LanMessage { kind = "hello", code = lan.RoomCode });
            Require(handshake.Length <= 1024 && !handshake.Contains("mapSettings") && !handshake.Contains("state"),
                "Control messages omit snapshot settings and stay below the host limit");
            LanMessage decoded = JsonUtility.FromJson<LanMessage>(handshake);
            Require(decoded.version == LanMessage.Version && decoded.code == lan.RoomCode && decoded.kind == "hello",
                "Compact control serialization preserves the protocol fields");
            string snapshotJson = LanMessageCodec.Serialize(new LanMessage { kind = "state", state = new LanSnapshot { seed = 42 } });
            Require(JsonUtility.FromJson<LanMessage>(snapshotJson).state.seed == 42,
                "State messages still carry map snapshots");
            guest.Send(handshake);
            float handshakeDeadline = Time.realtimeSinceStartup + 5f;
            while (lan.ConnectedPlayers < 2 && Time.realtimeSinceStartup < handshakeDeadline && !guest.Closed)
            { guest.Pump(_ => { }); yield return null; }
            Require(lan.ConnectedPlayers == 2, "Authenticated guest connects (closed=" + guest.Closed + "): " + lan.Status);
            Require(lan.StartHostedMatch(), "Authenticated guest starts a two-player match: " + lan.Status);
            while (lan.LatestState == null) { guest.Pump(_ => { }); yield return null; }
            var session = UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>();
            var map = UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>();
            var first = session.Participants[0];
            var second = session.Participants[1];
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 1, action = "roll", turn = 0, round = 1, player = 0 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(!first.HasRolled && !second.HasRolled, "Guest cannot roll for the host");
            lan.Submit("roll");
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            lan.Submit("end");
            while (lan.LatestState.turn != 1) { guest.Pump(_ => { }); yield return null; }
            Vector2Int before = second.GridPosition;
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 2, action = "move", turn = 1, round = 1, x = before.x + 1, y = before.y }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.GridPosition == before && !second.IsMoving, "Movement before rolling is rejected");
            Item custom = UnityEngine.Object.Instantiate(map.GetComponent<ItemSpawner>().GetNetworkPrefab(2));
            custom.PickUp(second.Inventory);
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 3, action = "item", turn = 1, round = 1, value = 999 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.Inventory.HasItem(custom) && !second.HasRolled, "Invalid Custom Dice value does not consume the item");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 4, action = "item", turn = 1, round = 1, value = 6 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.HasRolled && second.MovesRemaining == 6 && !second.Inventory.HasItem(custom), "Valid remote item works once");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 5, action = "move", turn = 1, round = 1, x = 999, y = -1 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.GridPosition == before && second.MovesRemaining == 6, "Out-of-bounds move is rejected");
            //Furniture requests use the same host checks as locker requests.
            var furniture = UnityEngine.Object.FindFirstObjectByType<SchoolFurniture>();
            var storage = furniture.GetComponent<Escape4Now.Obstacles.ObstacleStorage>();
            storage.SetStoredItem(map.GetComponent<ItemSpawner>().GetNetworkPrefab(1));
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 6, action = "take", turn = 1, round = 1,
                x = furniture.Cell.x, y = furniture.Cell.y }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(storage.HasItem && !second.Inventory.IsFull, "Remote search from a distant tile is rejected");
            bool adjacent = false;
            foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int cell = furniture.Cell + step;
                if (!map.IsWalkable(cell) || map.IsOccupied(cell)) continue;
                adjacent = second.SetGridPosition(cell);
                if (adjacent) break;
            }
            Require(adjacent, "Guest can approach furniture");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 7, action = "take", turn = 1, round = 1,
                x = furniture.Cell.x, y = furniture.Cell.y }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(!storage.HasItem && second.Inventory.IsFull, "Remote furniture pickup transfers one item");
            var collected = second.Inventory.Items[0];
            second.Inventory.RemoveItem(collected);
            UnityEngine.Object.Destroy(collected.gameObject);
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 8, action = "take", turn = 1, round = 1,
                x = furniture.Cell.x, y = furniture.Cell.y }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(!second.Inventory.IsFull, "Remote repeat pickup cannot duplicate an item");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 4, action = "end", turn = 1, round = 1 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(lan.LatestState.turn == 1, "Repeated sequence is rejected");
            //The new key uses a separate slot, including when the request comes from a guest.
            var keyLocker = Array.Find(map.GetComponentsInChildren<Escape4Now.Obstacles.ObstacleStorage>(),
                container => container.StoredItemPrefab is KeyItem);
            Require(keyLocker != null, "The host generated the key locker");
            Item keyPrefab = keyLocker.StoredItemPrefab;
            keyLocker.SetStoredItem(null);
            storage.SetStoredItem(keyPrefab);
            var regularItem = UnityEngine.Object.Instantiate(map.GetComponent<ItemSpawner>().GetNetworkPrefab(1));
            regularItem.PickUp(second.Inventory);
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 9, action = "take", turn = 1, round = 1,
                x = furniture.Cell.x, y = furniture.Cell.y }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.Inventory.HasKey() && second.Inventory.IsFull && !storage.HasItem
                && lan.LatestState.players[1].hasKey && LanWorldView.IsValid(lan.LatestState),
                "Remote key pickup works with a full item slot and valid snapshots");
            var key = second.Inventory.KeyItems[0];
            second.Inventory.RemoveKey();
            first.Inventory.GiveKey(key);
            first.SetGridPosition(second.GridPosition);
            Require(first.RecordSuccessfulTargetedAction(second), "The host records protection for the battle test");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 10, action = "battle", turn = 1, round = 1, value = 0 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(!second.IsInMenu() && first.Inventory.HasKey(), "A guest cannot battle a protected player");
            first.EventState.BeginTurn();
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 11, action = "battle", turn = 1, round = 1, value = 999 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(!second.IsInMenu() && first.Inventory.HasKey(), "Invalid battle target is rejected");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 12, action = "battle", turn = 1, round = 1, value = 0 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.IsInMenu() && !string.IsNullOrEmpty(lan.LatestState.battleMessage), "Guest can start a valid shared-tile battle");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 13, action = "battleClose", turn = 0, round = 1 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(second.IsInMenu(), "Battle results reject a forged turn owner");
            guest.Send(LanMessageCodec.Serialize(new LanMessage { kind = "command", sequence = 14, action = "battleClose", turn = 1, round = 1 }));
            for (int i = 0; i < 15; i++) { guest.Pump(_ => { }); yield return null; }
            Require(!second.IsInMenu() && string.IsNullOrEmpty(lan.LatestState.battleMessage), "Guest can close its confirmed battle result");
            guest.Dispose();
            for (int i = 0; i < 15; i++) yield return null;
            Require(lan.Status.Contains("disconnected") && Time.timeScale == 0f, "Disconnect stops the match clearly");
        }
        lan.Disconnect();
        Time.timeScale = 1f;
        Debug.Log("LAN AUTHORITY CHECKS PASSED");
    }

    private static LanConnection Connect()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(IPAddress.Loopback, LanMultiplayerSession.Port);
        return new LanConnection(socket);
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        Debug.Log("PASS: " + label);
    }
}
