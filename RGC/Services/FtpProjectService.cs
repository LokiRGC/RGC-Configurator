using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RGC.Models;

namespace RGC.Services
{
    public static class FtpProjectService
    {
        private static readonly string DataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RGC", "ftpprojects.json");

        public static List<FtpProject> LoadAll()
        {
            try
            {
                if (!File.Exists(DataFile)) return new();
                var json = File.ReadAllText(DataFile);
                return JsonSerializer.Deserialize<List<FtpProject>>(json) ?? new();
            }
            catch { return new(); }
        }

        public static void SaveAll(List<FtpProject> projects)
        {
            try
            {
                var dir = Path.GetDirectoryName(DataFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                File.WriteAllText(DataFile, JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static void Create(FtpProject p)
        {
            var list = LoadAll();
            list.Add(p);
            SaveAll(list);
        }

        public static void Update(FtpProject p)
        {
            var list = LoadAll();
            var idx = list.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) { list[idx] = p; SaveAll(list); }
        }

        public static void Delete(string id)
        {
            var list = LoadAll();
            list.RemoveAll(x => x.Id == id);
            SaveAll(list);
        }
    }
}
