using UnityEngine;

namespace Escape4Now.UI
{
    // Shared filled/dimmed hearts. Health continues to belong to PlayerCharacter.
    public static class HealthHeartText
    {
        public static string Format(int remaining, int maximum, string lostColor)
        {
            maximum = Mathf.Max(1, maximum);
            remaining = Mathf.Clamp(remaining, 0, maximum);
            string lost = new string ('♥', maximum - remaining);
            return new string ('♥', remaining) + (lost.Length > 0 ? "<color=" + lostColor + ">" + lost + "</color>" : "");
        }
    }
}
