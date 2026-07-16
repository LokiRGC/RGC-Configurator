using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RGC.Services
{
    public class PlayerStats
    {
        public string Name { get; set; } = "";
        public string SteamId { get; set; } = "";
        public TimeSpan TotalTime { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public string Icon => IsOnline ? "🟢" : "⚫";
        public string DisplayTime => TotalTime.Hours > 0
            ? $"{TotalTime.Hours}ч {TotalTime.Minutes}м"
            : $"{TotalTime.Minutes}м";
        public string DisplayLastSeen => LastSeen == DateTime.MinValue ? "-" : LastSeen.ToString("HH:mm");
    }

    public class ServerStatsService
    {
        public List<PlayerStats> GetPlayerStats(string profilesDir)
        {
            var admFiles = Directory.GetFiles(profilesDir, "*.adm", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(profilesDir, "*.ADM", SearchOption.TopDirectoryOnly))
                .OrderByDescending(f => f)
                .ToList();

            if (admFiles.Count == 0) return new List<PlayerStats>();

            var latest = admFiles.First();
            var lines = File.ReadAllLines(latest);

            var sessions = new Dictionary<string, List<TimeSpan>>();
            var playerNames = new Dictionary<string, string>();
            var connectTimes = new Dictionary<string, DateTime>();
            var lastSeen = new Dictionary<string, DateTime>();

            foreach (var line in lines)
            {
                var timeMatch = Regex.Match(line, @"^(\d{2}:\d{2}:\d{2})");
                if (!timeMatch.Success) continue;
                var time = timeMatch.Groups[1].Value;

                var playerMatch = Regex.Match(line, @"Player\s+""([^""]+)""\s+\(id=([^)\s]+)");
                if (!playerMatch.Success) continue;

                var name = playerMatch.Groups[1].Value;
                var id = playerMatch.Groups[2].Value;

                playerNames[id] = name;

                if (line.Contains("is connecting") || line.Contains("is connected"))
                {
                    if (DateTime.TryParse(time, out var dt))
                    {
                        connectTimes[id] = dt;
                        lastSeen[id] = dt;
                    }
                }
                else if (line.Contains("has been disconnected"))
                {
                    if (connectTimes.TryGetValue(id, out var connectTime))
                    {
                        if (DateTime.TryParse(time, out var disconnectTime))
                        {
                            if (!sessions.ContainsKey(id))
                                sessions[id] = new List<TimeSpan>();
                            sessions[id].Add(disconnectTime - connectTime);
                            connectTimes.Remove(id);
                            lastSeen[id] = disconnectTime;
                        }
                    }
                }
            }

            var stats = new List<PlayerStats>();
            foreach (var id in playerNames.Keys)
            {
                var total = sessions.TryGetValue(id, out var s) ? new TimeSpan(s.Select(ts => ts.Ticks).Sum()) : TimeSpan.Zero;
                stats.Add(new PlayerStats
                {
                    Name = playerNames[id],
                    SteamId = id,
                    TotalTime = total,
                    LastSeen = lastSeen.TryGetValue(id, out var ls) ? ls : DateTime.MinValue,
                    IsOnline = connectTimes.ContainsKey(id)
                });
            }

            return stats.OrderByDescending(s => s.TotalTime).ToList();
        }
    }
}
