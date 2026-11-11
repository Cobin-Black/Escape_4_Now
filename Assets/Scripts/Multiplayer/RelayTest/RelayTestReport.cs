using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Keep useful errors, but never export access tokens, Relay keys, or connection payloads.
    public sealed class RelayTestReport
    {
        private readonly List<string> entries = new List<string>();
        private readonly int maximumEntries;
        public string Recent => string.Join("\n", entries.GetRange(Math.Max(0, entries.Count - 12), Math.Min(12, entries.Count)));

        public RelayTestReport(int maximumEntries) { this.maximumEntries = maximumEntries; }

        public static string Safe(string message)
        {
            string clean = Regex.Replace(message ?? "", @"(?i)Bearer\s+\S+", "Bearer [redacted]");
            clean = Regex.Replace(clean, @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[token redacted]");
            clean = Regex.Replace(clean, "(?i)(accessToken|sessionToken|secretKey|authorization)([\\s\"':=]+)[^\\s,\"}]+", "$1$2[redacted]");
            return clean;
        }

        public void Add(string message)
        {
            string line = DateTime.UtcNow.ToString("HH:mm:ss") + " UTC  " + Safe(message);
            entries.Add(line);
            if (entries.Count > maximumEntries) entries.RemoveAt(0);
            Debug.Log("[Relay test] " + line);
        }

        public string Export(string summary, string networkLabel)
        {
            string directory = Path.Combine(Application.persistentDataPath, "RelayTests");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "relay-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            var text = new StringBuilder();
            text.AppendLine("Escape 4 Now - Relay connection test");
            text.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            text.AppendLine("Unity: " + Application.unityVersion + " | Platform: " + Application.platform);
            text.AppendLine("Project: " + Application.cloudProjectId + " | Build: " + Application.version);
            text.AppendLine("Network label (entered by tester): " + Safe(networkLabel));
            text.AppendLine(Safe(summary));
            text.AppendLine("This app cannot prove which Wi-Fi/network the other computer uses.");
            text.AppendLine("No gameplay synchronization is included in this diagnostic.");
            foreach (string line in entries) text.AppendLine(line);
            File.WriteAllText(path, text.ToString());
            return path;
        }
    }
}
