using System.IO;
using System.Text.Json;
using RGC.Models;

namespace RGC.Services
{
    public static class ProjectService
    {
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC");
        private static readonly string ProjectsFile = Path.Combine(DataDir, "projects.json");

        public static List<Project> LoadAll()
        {
            try
            {
                if (!File.Exists(ProjectsFile)) return new List<Project>();
                var json = File.ReadAllText(ProjectsFile);
                return JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();
            }
            catch { return new List<Project>(); }
        }

        public static void SaveAll(List<Project> projects)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var json = JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProjectsFile, json);
            }
            catch { }
        }

        public static Project? FindById(string id)
        {
            return LoadAll().FirstOrDefault(p => p.Id == id);
        }

        public static void Create(Project project)
        {
            var list = LoadAll();
            list.Add(project);
            SaveAll(list);
        }

        public static void Update(Project project)
        {
            var list = LoadAll();
            var idx = list.FindIndex(p => p.Id == project.Id);
            if (idx >= 0)
            {
                list[idx] = project;
                SaveAll(list);
            }
        }

        public static void Delete(string id)
        {
            var list = LoadAll();
            list.RemoveAll(p => p.Id == id);
            SaveAll(list);
        }
    }
}
