using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RGC.Services
{
    public class ModConfigMappingService
    {
        private readonly Dictionary<string, string> _index;
        private readonly List<(string pattern, string modName)> _patterns;

        public ModConfigMappingService(IEnumerable<string> modNames)
        {
            _index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _patterns = new List<(string, string)>();

            foreach (var modName in modNames)
            {
                var clean = modName.StartsWith("@") ? modName[1..] : modName;
                var normalized = Normalize(clean);

                if (!_index.ContainsKey(normalized))
                    _index[normalized] = modName;

                _patterns.Add((normalized, modName));
            }
        }

        private static string Normalize(string name)
        {
            return Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", "");
        }

        public string? FindModForFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return null;
            var normalized = Normalize(folderName);

            if (_index.TryGetValue(normalized, out var mod))
                return mod;

            foreach (var (pattern, modName) in _patterns)
            {
                if (normalized.Contains(pattern) || pattern.Contains(normalized))
                    return modName;
            }

            return null;
        }

        public string? FindModForFile(string fileName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            return FindModForFolder(nameWithoutExt);
        }
    }
}
