using System;
using System.Reflection;
using Escape4Now.Multiplayer;
using Escape4Now.Multiplayer.RelayTesting;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Checks saved references and protocol configuration without pretending to test another network.
public static class RelayFoundationChecks
{
    public static void Run()
    {
        foreach (string scene in new[] { RelayTestAuthoring.ScenePath, "Assets/Scenes/OnlineRace.unity" })
        {
            EditorSceneManager.OpenScene(scene);
            var relay = Object.FindFirstObjectByType<RelayRoomConnection>();
            Require(relay != null && relay.Settings != null && relay.Network != null, "Saved connection references: " + scene);
            var serialized = new SerializedObject(relay);
            Require(serialized.FindProperty("identity").objectReferenceValue != null, "Saved anonymous identity");
            var transport = (UnityTransport)serialized.FindProperty("transport").objectReferenceValue;
            Require(transport != null, "Saved transport");
            Require(Object.FindFirstObjectByType<InputSystemUIInputModule>() != null, "Input System UI module");
            Require(relay.Network.NetworkConfig.PlayerPrefab == null && !relay.Network.NetworkConfig.EnableSceneManagement,
                "No duplicate player spawning or automatic gameplay scene synchronization");
            foreach (var view in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(view is RelayTestView) && !(view is OnlineRaceLobbyView)) continue;
                var data = new SerializedObject(view);
                var field = data.GetIterator();
                while (field.NextVisible(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference)
                        Require(field.objectReferenceValue != null, view.GetType().Name + "/" + field.name + " is assigned");
            }
            foreach (var dropdown in Object.FindObjectsByType<Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Require(dropdown.template != null && dropdown.itemText != null && dropdown.captionText != null, "Usable saved dropdown");
            if (scene.Contains("OnlineRace"))
            {
                Require(Object.FindFirstObjectByType<OnlineRaceSession>() != null, "Online Race uses its own session adapter");
                Require(Object.FindFirstObjectByType<LanMultiplayerSession>() != null, "Existing authoritative Race system is reused");
                Require(relay.Settings.maximumPlayers == 4, "Online Race supports up to four players");
            }
        }
        string safe = RelayTestReport.Safe("Bearer secret-token accessToken=topsecret eyJabc.def.ghi");
        Require(!safe.Contains("secret-token") && !safe.Contains("topsecret") && !safe.Contains("eyJabc"), "Report redacts credentials");
        Require(RelayTestReport.Safe("RelayServiceException: request denied (403)").Contains("403"), "Actual service error survives redaction");
        Debug.Log("RELAY FOUNDATION CHECKS PASSED — no cross-network result is claimed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Relay foundation check failed: " + message);
    }
}
