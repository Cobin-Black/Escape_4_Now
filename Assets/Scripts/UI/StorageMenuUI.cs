// Shows the open obstacle's stored item and forwards Take or Close to the storage component.

using Escape4Now.Items;
using Escape4Now.Obstacles;
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

        private void LateUpdate()
        {
            var storage = ObstacleStorage.OpenStorage;
            panel.SetActive(context.Playing && !context.Finished && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused && storage != null);
            if (!panel.activeSelf) return;
            title.text = storage.name;
            var item = storage.StoredItemPrefab;
            var inventory = storage.ViewingPlayer != null ? storage.ViewingPlayer.Inventory : null;
            bool canTake = item != null && inventory != null
                && (item is KeyItem ? !inventory.HasKey() : !inventory.IsFull);
            body.text = item == null ? emptyText : string.Format(contentsFormat, item.ItemName, item.Description)
                + (canTake ? "" : "\n\n" + fullText);
            take.interactable = canTake;
        }

        public void Take() { ObstacleStorage.OpenStorage?.ConfirmMenu(); }
        public void Close() { ObstacleStorage.OpenStorage?.DismissMenu(); }
    }
}
