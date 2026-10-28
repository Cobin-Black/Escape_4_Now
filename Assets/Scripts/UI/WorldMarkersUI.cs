// Places key and stored-item markers on screen and spaces them so they do not cover names or one another.

using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.UI
{
    public sealed class WorldMarkersUI : MonoBehaviour
    {
        [Header("Scene and Saved Prefab References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private WorldMarkerUI keyBadgePrefab;
        [SerializeField] private WorldMarkerUI storageMarkerPrefab;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private ActivePlayerNameUI activePlayerName;
        [SerializeField, Min(0f), Tooltip("Screen-pixel clearance when a nearby key badge would cover the active player's name.")]
        private float nameBadgeSpacing = 3f;
        [Header("World Offsets")]
        [SerializeField] private Vector3 keyOffset = new Vector3(0f, 1.05f, 0f);
        [SerializeField] private Vector3 storageOffset = new Vector3(0f, 0.45f, 0f);
        [SerializeField, Min(0f), Tooltip("Minimum screen-pixel gap between reveal tags. Nearby tags stack only when needed.")]
        private float storageMarkerSpacing = 2f;
        [SerializeField, Min(0.05f)] private float refreshSeconds = 0.5f;
        [Header("Storage Assist Labels")]
        [SerializeField] private string keyLabel = "KEY";
        [SerializeField] private string itemLabel = "ITEM";
        [SerializeField] private Color keyColor = new Color(1f, 0.78f, 0.2f);
        [SerializeField] private Color itemColor = new Color(0.35f, 0.85f, 1f);
        private readonly Dictionary<PlayerCharacter, WorldMarkerUI> badges = new Dictionary<PlayerCharacter, WorldMarkerUI>();
        private readonly Dictionary<ObstacleStorage, WorldMarkerUI> storage = new Dictionary<ObstacleStorage, WorldMarkerUI>();
        private readonly List<WorldMarkerUI> visibleStorageMarkers = new List<WorldMarkerUI>();
        private readonly List<Rect> placedStorageBounds = new List<Rect>();
        private float nextScan;

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + refreshSeconds;
                foreach (var player in FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
                    if (!badges.ContainsKey(player)) badges.Add(player, Instantiate(keyBadgePrefab, markerRoot));
                foreach (var obstacle in FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None))
                    if (!storage.ContainsKey(obstacle)) storage.Add(obstacle, Instantiate(storageMarkerPrefab, markerRoot));
            }
            var race = context.Session != null ? context.Session.Mode as Race_Gamemode : null;
            var keyLook = race != null && race.KeyPrefab != null ? race.KeyPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            foreach (var pair in badges)
            {
                bool show = context.Playing && !context.Finished && pair.Key != null
                    && pair.Key.isActiveAndEnabled && pair.Key.HasKey && keyLook != null
                    && (activePlayerName == null || !activePlayerName.ShowsKeyFor(pair.Key));
                UpdateMarker(pair.Value, show, pair.Key != null ? pair.Key.transform.position + keyOffset : Vector3.zero,
                    keyLook != null ? keyLook.sprite : null, keyLook != null ? keyLook.color : Color.white, "", keyColor);
                if (activePlayerName != null && pair.Value.gameObject.activeInHierarchy)
                {
                    Rect nameBounds = activePlayerName.ScreenBounds;
                    Rect badgeBounds = pair.Value.ScreenBounds;
                    if (nameBounds.width > 0f && badgeBounds.xMin < nameBounds.xMax + nameBadgeSpacing
                        && badgeBounds.xMax > nameBounds.xMin - nameBadgeSpacing
                        && badgeBounds.yMin < nameBounds.yMax + nameBadgeSpacing
                        && badgeBounds.yMax > nameBounds.yMin - nameBadgeSpacing)
                        pair.Value.MoveBottomTo(nameBounds.yMax + nameBadgeSpacing);
                }
            }
            foreach (var pair in storage)
            {
                bool key = pair.Key != null && pair.Key.ContainsKey && LocalMultiplayerSession.ShowKeyLocation;
                bool item = pair.Key != null && pair.Key.HasItem && LocalMultiplayerSession.ShowItemObstacles;
                UpdateMarker(pair.Value, context.Playing && !context.Finished && (key || item),
                    pair.Key != null ? pair.Key.transform.position + storageOffset : Vector3.zero,
                    null, Color.white, key ? keyLabel : itemLabel, key ? keyColor : itemColor);
            }
            ArrangeStorageMarkers();
        }

        // Spread visible storage labels apart so nearby item containers do not cover one another.
        private void ArrangeStorageMarkers()
        {
            visibleStorageMarkers.Clear();
            placedStorageBounds.Clear();
            foreach (var pair in storage)
                if (pair.Value.gameObject.activeInHierarchy) visibleStorageMarkers.Add(pair.Value);
            visibleStorageMarkers.Sort((left, right) => left.ScreenBounds.yMin.CompareTo(right.ScreenBounds.yMin));
            foreach (var marker in visibleStorageMarkers)
            {
                Rect bounds = marker.ScreenBounds;
                //Place lower tags first and lift only labels that would touch an existing tag.
                for (int attempt = 0; attempt < placedStorageBounds.Count; attempt++)
                {
                    float bottom = bounds.yMin;
                    foreach (Rect other in placedStorageBounds)
                        if (bounds.xMin < other.xMax + storageMarkerSpacing && bounds.xMax > other.xMin - storageMarkerSpacing
                            && bounds.yMin < other.yMax + storageMarkerSpacing && bounds.yMax > other.yMin - storageMarkerSpacing)
                            bottom = Mathf.Max(bottom, other.yMax + storageMarkerSpacing);
                    if (bottom <= bounds.yMin) break;
                    marker.MoveBottomTo(bottom);
                    bounds = marker.ScreenBounds;
                }
                placedStorageBounds.Add(bounds);
            }
        }

        private void UpdateMarker(WorldMarkerUI marker, bool show, Vector3 world, Sprite sprite, Color tint, string label, Color color)
        {
            var camera = context.WorldCamera;
            Vector3 screen = camera != null ? camera.WorldToScreenPoint(world) : Vector3.zero;
            marker.gameObject.SetActive(show && camera != null && screen.z > 0f);
            if (marker.gameObject.activeSelf) marker.Present(screen, sprite, tint, label, color);
        }
    }
}
