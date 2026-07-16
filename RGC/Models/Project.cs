namespace RGC.Models
{
    public class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string ServerPath { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastOpenedAt { get; set; } = DateTime.Now;
    }
}
