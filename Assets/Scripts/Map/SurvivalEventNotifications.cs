// Keeps active-effect messages until the effect ends and gives instant results a short display time.

using System;
using System.Collections.Generic;
using System.Text;

namespace Escape4Now.Map
{
    //Presentation lifetime follows the actual effect; instant results use a short, paused toast.
    public sealed class SurvivalEventNotifications
    {
        private sealed class Entry
        {
            public string Text;
            public Func<bool> IsActive;
            public float Seconds;
        }
        private readonly List<Entry> entries = new List<Entry>();
        // Replace an identical notice instead of filling the HUD with copies.
        public void Add(string text, float seconds, Func<bool> active = null)
        {
            entries.RemoveAll(entry => entry.Text == text);
            entries.Add(new Entry { Text = text, Seconds = seconds, IsActive = active });
        }
        // Ongoing notices use their active-state check; only immediate results use elapsed seconds.
        public string Tick(float seconds, int visibleLimit, string overflowFormat)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                entry.Seconds -= seconds;
                if (entry.IsActive != null ? !entry.IsActive() : entry.Seconds <= 0) entries.RemoveAt(i);
            }
            var text = new StringBuilder();
            // Limit what is drawn without deleting older effects that are still active.
            int first = Math.Max(0, entries.Count - Math.Max(1, visibleLimit));
            for (int i = first; i < entries.Count; i++)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(entries[i].Text);
            }
            if (first > 0) text.Append('\n').AppendFormat(overflowFormat, first);
            return text.ToString();
        }
    }
}
