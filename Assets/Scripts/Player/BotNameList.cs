using System;
using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Player
{
    //The names bots can get, editable in Assets/Resources/Bot Names.asset.
    //Every mode asks this for its bots' names when a match starts, locally and on an online host.
    [CreateAssetMenu(menuName = "Escape 4 Now/Bot Names", fileName = "Bot Names")]
    public sealed class BotNameList : ScriptableObject
    {
        //Player names are cut at 16 characters, so the bot tag must fit inside that.
        public const int MaxLength = 16;

        [SerializeField, Tooltip("Names a bot can get. Keep them short (10 characters or fewer) so the bot tag fits.")]
        private string[] names = DefaultNames;
        [SerializeField, Tooltip("How a bot's name is shown. {0} is the picked name; keep the tag so players know it is a bot.")]
        private string format = "{0} (BOT)";

        private static readonly string[] DefaultNames =
        {
            "Pop Quiz", "Hall Pass", "Recess", "Doodle", "Sparky", "Crayon", "Eraser", "Mr Chalk",
            "Ms Ruler", "Glue Stick", "Lunchbox", "Homework", "Backpack", "Detention", "Gym Sock",
            "Pencil", "Sharpie", "Locker", "Field Trip", "Spitball", "Paperclip", "Bookworm", "Snack Time"
        };

        //A separate generator, so naming bots never changes the game's own random map or item choices.
        private static readonly System.Random Picker = new System.Random();
        private static BotNameList loaded;
        private static BotNameList Settings => loaded != null ? loaded : loaded = Resources.Load<BotNameList>("Bot Names");

        //Returns the names with each bot seat given a different random name that no player is using.
        public static string[] Assign(bool[] bots, string[] current)
        {
            var result = (string[])current.Clone();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < current.Length; i++)
                if (i >= bots.Length || !bots[i]) taken.Add(current[i] ?? "");

            BotNameList settings = Settings;
            var pool = new List<string>();
            foreach (string name in settings != null && settings.names != null && settings.names.Length > 0 ? settings.names : DefaultNames)
                if (!string.IsNullOrWhiteSpace(name)) pool.Add(name.Trim());
            //Shuffle so each match gets different bots.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Picker.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            string pattern = settings != null && !string.IsNullOrWhiteSpace(settings.format) ? settings.format : "{0} (BOT)";
            int next = 0;
            for (int slot = 0; slot < current.Length && slot < bots.Length; slot++)
            {
                if (!bots[slot]) continue;
                string picked = null;
                while (picked == null && next < pool.Count)
                {
                    string candidate = Format(pattern, pool[next++]);
                    if (taken.Add(candidate)) picked = candidate;
                }
                //More bots than names: fall back to the seat number.
                if (picked == null)
                {
                    picked = "BOT " + (slot + 1);
                    taken.Add(picked);
                }
                result[slot] = picked;
            }
            return result;
        }

        //Shortens the picked name, not the bot tag, when the result would be longer than a player name.
        private static string Format(string pattern, string name)
        {
            string text;
            try { text = string.Format(pattern, name).Trim(); }
            catch (FormatException) { text = name + " (BOT)"; pattern = "{0} (BOT)"; }
            if (text.Length <= MaxLength) return text;
            int tag = text.Length - name.Length;
            string shorter = name.Substring(0, Math.Max(1, MaxLength - tag)).Trim();
            try { text = string.Format(pattern, shorter).Trim(); }
            catch (FormatException) { text = shorter; }
            return text.Length <= MaxLength ? text : text.Substring(0, MaxLength);
        }
    }
}
