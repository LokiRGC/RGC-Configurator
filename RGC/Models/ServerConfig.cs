namespace RGC.Models
{
    public class ServerConfig
    {
        // serverDZ.cfg
        public string Hostname { get; set; } = "My DayZ Server";
        public string Password { get; set; } = "";
        public string PasswordAdmin { get; set; } = "";
        public string Description { get; set; } = "";
        public bool EnableWhitelist { get; set; }
        public int MaxPlayers { get; set; } = 60;
        public int VerifySignatures { get; set; } = 2;
        public bool ForceSameBuild { get; set; } = true;
        public bool DisableVoN { get; set; }
        public int VonCodecQuality { get; set; } = 20;
        public string ShardId { get; set; } = "";
        public bool Disable3rdPerson { get; set; }
        public bool DisableCrosshair { get; set; }
        public bool DisablePersonalLight { get; set; } = true;
        public int LightingConfig { get; set; }
        public string ServerTime { get; set; } = "SystemTime";
        public double ServerTimeAcceleration { get; set; } = 12;
        public double ServerNightTimeAcceleration { get; set; } = 1;
        public bool ServerTimePersistent { get; set; }
        public int LoginQueueConcurrentPlayers { get; set; } = 5;
        public int LoginQueueMaxPlayers { get; set; } = 500;
        public int InstanceId { get; set; } = 1;
        public bool StorageAutoFix { get; set; } = true;
        public string MissionTemplate { get; set; } = "dayzOffline.chernarusplus";
        public string RConPassword { get; set; } = "";

        // StartBat.cmd
        public string BatchServerName { get; set; } = "My DayZ Server";
        public string ServerLocation { get; set; } = "";
        public int ServerPort { get; set; } = 2302;
        public string ConfigFileName { get; set; } = "serverDZ.cfg";
        public int CpuCores { get; set; } = 2;
        public string ModList { get; set; } = "";
        public string ServerModList { get; set; } = "";
        public int RestartInterval { get; set; } = 14390;
    }
}
