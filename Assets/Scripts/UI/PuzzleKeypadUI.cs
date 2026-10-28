// Edits the code field through the saved keypad buttons, using Unity UI for input.

using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class PuzzleKeypadUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private InputField input;

        public void AppendDigit(string digit)
        {
            if (input == null || string.IsNullOrEmpty(digit)) return;
            foreach (char character in digit)
                if (character < '0' || character > '9') return;
            if (input.characterLimit > 0 && input.text.Length + digit.Length > input.characterLimit) return;
            input.text += digit;
        }

        public void DeleteDigit()
        {
            if (input != null && input.text.Length > 0) input.text = input.text.Substring(0, input.text.Length - 1);
        }

        public void Clear()
        {
            if (input != null) input.text = "";
        }
    }
}
