using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Object;

namespace Escape4Now.Multiplayer
{
    // Reads Race controls and checks Race commands. LAN and Relay use the same rules.
    internal sealed class RaceNetworkActions
    {
        private readonly LanMultiplayerSession session;
        private readonly List<int> raceBattleTargets = new();
        private bool choosingNumber;
        public IReadOnlyList<int> BattleTargets => raceBattleTargets;
        public int SelectedDiceNumber { get; private set; } = 1;

        public RaceNetworkActions(LanMultiplayerSession session) => this.session = session;

        public void ResetSelection()
        {
            choosingNumber = false;
            raceBattleTargets.Clear();
        }

        // Only the host runs these actions. It checks the turn before changing the match.
        public bool Execute(LocalMultiplayerSession local, int slot, LanMessage message)
        {
            TurnSystemController turns = FindFirstObjectByType<TurnSystemController>();
            PlayerCharacter player = local.Participants[slot];
            if (turns.CurrentPlayerIndex != slot || message.turn != slot || message.round != turns.TurnNumber
                || player.HasReachedExit || player.IsMoving) return false;
            var battle = FindFirstObjectByType<Race_Gamemode>();
            if (message.action == "battleClose")
            {
                battle?.FinishNetworkBattle(player);
                return true;
            }
            if (session.UsesRaceMatchUI && ObstacleStorage.OpenStorage != null)
            {
                var storage = ObstacleStorage.OpenStorage;
                if (storage.ViewingPlayer != player) return false;
                if (message.action == "storageConfirm") storage.ConfirmMenu();
                if (message.action == "storageClose") storage.DismissMenu();
                return true;
            }
            if (player.IsInMenu() || player.IsUsingItem()) return false;
            switch (message.action)
            {
                case "roll": player.RollDice(); break;
                case "move":
                    if (message.x >= 0 && message.y >= 0 && message.x < SchoolMapLayout.Size && message.y < SchoolMapLayout.Size)
                        player.MoveToGridPosition(new Vector2Int(message.x, message.y));
                    break;
                case "end": turns.AdvanceTurn(); break;
                case "battle":
                    if (message.value >= 0 && message.value < local.Participants.Count)
                        battle?.TryNetworkBattle(player, local.Participants[message.value]);
                    break;
                case "item":
                    if (!player.CanUseItem() || player.Inventory.Items.Count == 0) break;
                    Item item = player.Inventory.Items[0];
                    if (item is CustomDiceItem)
                    {
                        if (message.value < 1 || message.value > 6) break;
                        player.SetMovesFromItem(message.value);
                        player.Inventory.RemoveItem(item);
                    }
                    else if (item is DoubleDiceItem || item is KeyStealerItem) item.Use(player.Inventory);
                    break;
                case "inspect" when session.UsesRaceMatchUI:
                case "take":
                    foreach (ObstacleStorage storage in FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None))
                    {
                        if (storage.GridPosition == new Vector2Int(message.x, message.y))
                        {
                            if (session.UsesRaceMatchUI) storage.InspectNetworkItem(player);
                            else storage.TakeNetworkItem(player);
                            break;
                        }
                    }
                    break;
            }
            return true;
        }

        // Read the scene's Input Actions. Menus take priority over moving and rolling.
        public void ReadInput()
        {
            if (!session.IsMyTurn)
            {
                ResetSelection();
                return;
            }

            LanSnapshot state = session.LatestState;
            int slot = session.LocalPlayerIndex;
            LanPlayerState player = state.players[slot];
            if (session.MatchPaused || Time.timeScale <= 0f || player.moving) return;
            var bindings = LocalMultiplayerSession.InputBindings;
            if (bindings == null) return;
            bool confirm = bindings.KeyBattle?.WasPressedThisFrame() == true;
            if (!string.IsNullOrEmpty(state.battleMessage))
            {
                //Wait for the dice above the players to land before the result can be closed.
                if (confirm && !Escape4Now.UI.RaceBattleDice.Rolling) session.Submit("battleClose");
                return;
            }
            if (session.ShowingRaceStorage)
            {
                if (bindings.Interact?.WasPressedThisFrame() == true) session.Submit("storageConfirm");
                return;
            }
            if (session.UsesRaceMatchUI && choosingNumber)
            {
                if (bindings.ItemOptionSelect?.WasPressedThisFrame() == true)
                    StepDiceNumber(Math.Sign(bindings.ItemOptionSelect.ReadValue<Vector2>().x));
                if (bindings.UseItem?.WasPressedThisFrame() == true) ChooseNumber(SelectedDiceNumber);
                return;
            }
            if (raceBattleTargets.Count > 0)
            {
                if (confirm) raceBattleTargets.Clear();
                return;
            }
            if (confirm)
            {
                for (int i = 0; i < state.players.Length; i++)
                {
                    LanPlayerState target = state.players[i];
                    if (i != slot && !player.hasKey && target.hasKey && !target.protectedFromPlayers
                        && target.x == player.x && target.y == player.y)
                    {
                        if (!session.UsesRaceMatchUI) { session.Submit("battle", value: i); return; }
                        raceBattleTargets.Add(i);
                    }
                }
                // Interact shares this key, so the press only stops here when there is someone to battle.
                if (raceBattleTargets.Count > 0)
                {
                    if (raceBattleTargets.Count == 1) ChooseRaceOpponent(0);
                    return;
                }
            }
            if (bindings.RollDice?.WasPressedThisFrame() == true) session.Submit("roll");
            if (bindings.UseItem?.WasPressedThisFrame() == true) UseItem();
            if (bindings.Interact?.WasPressedThisFrame() == true) TakeNearbyItem();
            InputAction move = bindings.Move;
            if (!choosingNumber && move != null && move.WasPressedThisFrame())
            {
                Vector2 input = move.ReadValue<Vector2>();
                Vector2Int direction = Mathf.Abs(input.y) >= Mathf.Abs(input.x)
                    ? new Vector2Int(0, Math.Sign(input.y)) : new Vector2Int(Math.Sign(input.x), 0);
                session.Submit("move", player.x + direction.x, player.y + direction.y);
            }
            if (bindings.Click?.WasPressedThisFrame() != true || bindings.Point == null || Camera.main == null) return;
            Vector2 position = bindings.Point.ReadValue<Vector2>();
            if (UiPointer.IsOverUI(position) || choosingNumber) return;
            IsometricMapTemplate map = FindFirstObjectByType<IsometricMapTemplate>();
            if (map.TryGetGridPosition(Camera.main.ScreenToWorldPoint(position), out Vector2Int cell))
            {
                foreach (LanObjectState item in state.objects)
                    if ((item.kind == 1 || item.kind == 3) && item.x == cell.x && item.y == cell.y)
                    { session.Submit(session.UsesRaceMatchUI ? "inspect" : "take", cell.x, cell.y); return; }
                session.Submit("move", cell.x, cell.y);
            }
        }

        // The number selector stays on this computer until the player confirms.
        public void UseItem()
        {
            if (!session.IsMyTurn || session.MatchPaused || session.LatestState.players[session.LocalPlayerIndex].rolled) return;
            if (session.LatestState.players[session.LocalPlayerIndex].item == 2)
            {
                SelectedDiceNumber = 1;
                choosingNumber = true;
            }
            else session.Submit("item");
        }

        // Send the location of a nearby locker. The host checks that the player can reach it.
        public void TakeNearbyItem()
        {
            if (!session.IsMyTurn || session.MatchPaused) return;
            LanPlayerState player = session.LatestState.players[session.LocalPlayerIndex];
            foreach (LanObjectState item in session.LatestState.objects)
                if ((item.kind == 1 || item.kind == 3) && item.interactable && Math.Abs(item.x - player.x) + Math.Abs(item.y - player.y) == 1)
                { session.Submit(session.UsesRaceMatchUI ? "inspect" : "take", item.x, item.y); return; }
        }

        public bool ChoosingNumber => choosingNumber;
        public void CancelDiceNumber() => choosingNumber = false;
        public void StepDiceNumber(int direction) => SelectedDiceNumber = (SelectedDiceNumber + direction + 5) % 6 + 1;

        public void ChooseRaceOpponent(int index)
        {
            if (!session.IsMyTurn || index < 0 || index >= raceBattleTargets.Count) return;
            session.Submit("battle", value: raceBattleTargets[index]);
            raceBattleTargets.Clear();
        }

        public void ChooseNumber(int value)
        {
            if (!choosingNumber || !session.IsMyTurn || session.MatchPaused || value < 1 || value > 6) return;
            session.Submit("item", value: value);
            choosingNumber = false;
        }
    }
}
