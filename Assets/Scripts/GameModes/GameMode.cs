using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.GameModes
{
    //Every gamemode the item spawner keeps a separate item list for.
    public enum GameModeType { Race, Monster, FreeForAll, Untitled }

    //Base for the scene's gamemode script. Each gamemode decides its own exit and win rules.
    public abstract class GameMode : MonoBehaviour
    {
        public abstract GameModeType Type { get; }

        private static GameMode current;

        //The gamemode script in the open scene, or null when the scene has none (such as the tutorial).
        public static GameMode Current => current != null ? current : (current = FindFirstObjectByType<GameMode>());

        //Whether this player may walk through the exit door.
        public virtual bool CanUseExit(PlayerCharacter player) => true;

        //Text for the HUD's objective box. Empty hides the box.
        public virtual string GetObjective(PlayerCharacter player) => "";

        //Lets the gamemode put its own items (like the Race key) into obstacles before random items are added.
        public virtual void PlaceGuaranteedItems(Escape4Now.Items.ItemSpawner spawner) { }

        //Reports the winning player once the match is over.
        public virtual bool TryGetWinner(System.Collections.Generic.IReadOnlyList<PlayerCharacter> players, out PlayerCharacter winner)
        {
            winner = null;
            if (players == null) return false;
            foreach (PlayerCharacter player in players)
                if (player != null && player.HasReachedExit)
                {
                    winner = player;
                    return true;
                }
            return false;
        }
    }
}
