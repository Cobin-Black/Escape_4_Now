// Shows the open obstacle's stored item and forwards Take or Close to the storage component.

using Escape4Now.Items;
using Escape4Now.Obstacles;
using Escape4Now.Multiplayer;
using Escape4Now.Map;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class StorageMenuUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private Button take;
        [Header("Copy")]
        [SerializeField, TextArea] private string emptyText = "It's empty.";
        [SerializeField, TextArea] private string contentsFormat = "Inside: {0}\n{1}";
        [SerializeField, TextArea] private string fullText = "You can only carry one item.";
        private ItemSpawner networkItems;

        // Refresh the open storage display and route its controls through the current match authority.
        private void LateUpdate()
        {
            var storage = ObstacleStorage.OpenStorage;
            var network = LanMultiplayerSession.Instance;
            bool online = network?.UsesRaceMatchUI == true;
            panel.SetActive(context.Playing && !context.Finished && !LocalMultiplayerSession.GameplayPaused
                && (online ? network.ShowingRaceStorage : storage != null));
            if (!panel.activeSelf) return;
            if (online)
            {
                var snapshot = network.LatestState;
                networkItems ??= (context.Session.Mode.GridMap as IsometricMapTemplate)?.GetComponent<ItemSpawner>();
                var contents = networkItems?.GetNetworkPrefab(snapshot.storageItem);
                var player = snapshot.players[network.LocalPlayerIndex];
                Present(snapshot.storageName, contents, contents is KeyItem ? !player.hasKey : player.item == 0);
                return;
            }
            title.text = storage.name;
            var item = storage.StoredItemPrefab;
            var inventory = storage.ViewingPlayer != null ? storage.ViewingPlayer.Inventory : null;
            bool canTake = item != null && inventory != null
                && (item is KeyItem ? !inventory.HasKey() : !inventory.IsFull);
            Present(storage.name, item, canTake);
        }

        // Display the selected storage contents using the existing item widgets.
        private void Present(string storageName, Item item, bool canTake)
        {
            title.text = storageName;
            body.text = item == null ? emptyText : string.Format(contentsFormat, item.ItemName, item.Description)
                + (canTake ? "" : "\n\n" + fullText);
            take.interactable = item != null && canTake;
        }

        // Take the displayed item through the host command online or the local storage action.
        public void Take()
        {
            var network = LanMultiplayerSession.Instance;
            if (network?.UsesRaceMatchUI == true) network.Submit("storageConfirm");
            else ObstacleStorage.OpenStorage?.ConfirmMenu();
        }

        // Close the open storage through the appropriate local or online menu path.
        public void Close()
        {
            var network = LanMultiplayerSession.Instance;
            if (network?.UsesRaceMatchUI == true) network.Submit("storageClose");
            else ObstacleStorage.OpenStorage?.DismissMenu();
        }
    }
}
