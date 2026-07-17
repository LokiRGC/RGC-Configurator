using System;
using System.Collections.Generic;

namespace RGC.Models
{
    public class FtpProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 21;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string RemotePath { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastOpenedAt { get; set; } = DateTime.Now;
    }

    public class ToolItem
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Description { get; set; } = "";
        public string Action { get; set; } = ""; // key for action handler
    }
}
