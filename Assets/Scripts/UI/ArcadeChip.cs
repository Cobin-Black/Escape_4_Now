using Escape4Now.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //Colors a player-count chip saved by the lobby editor scripts, so it lights up like the Race and Survival lobby.
    //The chip has a "Selected Shadow", a "Border" and a "Face" image, plus its labels.
    public static class ArcadeChip
    {
        public static void Paint(Transform chip, bool selected)
        {
            Paint(chip, selected, ArcadeMenuKit.Teal);
        }

        public static void Paint(Transform chip, bool selected, Color accent)
        {
            if (chip == null) return;
            var shadow = chip.Find("Selected Shadow");
            if (shadow != null) shadow.gameObject.SetActive(selected);
            SetColor(chip.Find("Border"), selected ? accent : ArcadeLobbyScreen.EdgeBlue);
            SetColor(chip.Find("Face"), selected ? accent : ArcadeLobbyScreen.CardNavy);
            foreach (var text in chip.GetComponentsInChildren<Text>(true))
                text.color = selected ? ArcadeMenuKit.Ink : Color.white;
        }

        private static void SetColor(Transform target, Color color)
        {
            var graphic = target != null ? target.GetComponent<Graphic>() : null;
            if (graphic != null) graphic.color = color;
        }
    }
}
