using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RGC.Models;

namespace RGC.Services
{
    public class DayZConfigService
    {
        public ServerConfig ReadConfig(string serverDir)
        {
            var config = new ServerConfig { ServerLocation = serverDir };
            var cfgPath = Path.Combine(serverDir, "serverDZ.cfg");
            var batPath = Path.Combine(serverDir, "StartBat.cmd");

            if (File.Exists(cfgPath))
                ParseCfg(cfgPath, config);

            if (File.Exists(batPath))
                ParseBat(batPath, config);

            return config;
        }

        public void WriteConfig(ServerConfig config)
        {
            if (string.IsNullOrEmpty(config.ServerLocation) || !Directory.Exists(config.ServerLocation))
                throw new DirectoryNotFoundException("Папка сервера не найдена.");

            WriteCfg(Path.Combine(config.ServerLocation, "serverDZ.cfg"), config);
            WriteBat(Path.Combine(config.ServerLocation, "StartBat.cmd"), config);
        }

        private void ParseCfg(string path, ServerConfig config)
        {
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("class") || trimmed.StartsWith("{")) continue;

                var match = Regex.Match(trimmed, @"^(\w+)\s*=\s*(.+?);");
                if (!match.Success) continue;

                var key = match.Groups[1].Value;
                var val = match.Groups[2].Value.Trim('"', ' ');

                switch (key)
                {
                    case "hostname": config.Hostname = val; break;
                    case "password": config.Password = val; break;
                    case "passwordAdmin": config.PasswordAdmin = val; break;
                    case "description": config.Description = val; break;
                    case "enableWhitelist": config.EnableWhitelist = val == "1"; break;
                    case "maxPlayers": int.TryParse(val, out var mp); config.MaxPlayers = mp; break;
                    case "verifySignatures": int.TryParse(val, out var vs); config.VerifySignatures = vs; break;
                    case "forceSameBuild": config.ForceSameBuild = val == "1"; break;
                    case "disableVoN": config.DisableVoN = val == "1"; break;
                    case "vonCodecQuality": int.TryParse(val, out var vcq); config.VonCodecQuality = vcq; break;
                    case "shardId": config.ShardId = val; break;
                    case "disable3rdPerson": config.Disable3rdPerson = val == "1"; break;
                    case "disableCrosshair": config.DisableCrosshair = val == "1"; break;
                    case "disablePersonalLight": config.DisablePersonalLight = val == "1"; break;
                    case "lightingConfig": int.TryParse(val, out var lc); config.LightingConfig = lc; break;
                    case "serverTime": config.ServerTime = val; break;
                    case "serverTimeAcceleration": double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sta); config.ServerTimeAcceleration = sta; break;
                    case "serverNightTimeAcceleration": double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var snta); config.ServerNightTimeAcceleration = snta; break;
                    case "serverTimePersistent": config.ServerTimePersistent = val == "1"; break;
                    case "loginQueueConcurrentPlayers": int.TryParse(val, out var lqcp); config.LoginQueueConcurrentPlayers = lqcp; break;
                    case "loginQueueMaxPlayers": int.TryParse(val, out var lqmp); config.LoginQueueMaxPlayers = lqmp; break;
                    case "instanceId": int.TryParse(val, out var iid); config.InstanceId = iid; break;
                    case "storageAutoFix": config.StorageAutoFix = val == "1"; break;
                    case "RConPassword": config.RConPassword = val; break;
                }

                if (key == "template")
                {
                    config.MissionTemplate = val;
                }
            }
        }

        private void ParseBat(string path, ServerConfig config)
        {
            var text = File.ReadAllText(path);
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                var nameMatch = Regex.Match(trimmed, @"^set serverName=(.*)$");
                if (nameMatch.Success) { config.BatchServerName = nameMatch.Groups[1].Value.Trim('"'); continue; }

                var locMatch = Regex.Match(trimmed, @"^set serverLocation=""(.*)""$");
                if (locMatch.Success) { config.ServerLocation = locMatch.Groups[1].Value; continue; }

                var portMatch = Regex.Match(trimmed, @"^set serverPort=(\d+)$");
                if (portMatch.Success) { int.TryParse(portMatch.Groups[1].Value, out var p); config.ServerPort = p; continue; }

                var cfgMatch = Regex.Match(trimmed, @"^set serverConfig=(.*)$");
                if (cfgMatch.Success) { config.ConfigFileName = cfgMatch.Groups[1].Value.Trim('"'); continue; }

                var cpuMatch = Regex.Match(trimmed, @"^set serverCPU=(\d+)$");
                if (cpuMatch.Success) { int.TryParse(cpuMatch.Groups[1].Value, out var c); config.CpuCores = c; continue; }

                var modMatch = Regex.Match(trimmed, @"-mod=""([^""]*)""");
                if (modMatch.Success) { config.ModList = modMatch.Groups[1].Value; }

                var srvModMatch = Regex.Match(trimmed, @"-servermod=""([^""]*)""");
                if (srvModMatch.Success) { config.ServerModList = srvModMatch.Groups[1].Value; }

                var timeoutMatch = Regex.Match(trimmed, @"^timeout (\d+)");
                if (timeoutMatch.Success) { int.TryParse(timeoutMatch.Groups[1].Value, out var t); config.RestartInterval = t; }
            }
        }

        private void WriteCfg(string path, ServerConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"hostname = \"{config.Hostname}\";");
            sb.AppendLine($"password = \"{config.Password}\";");
            sb.AppendLine($"passwordAdmin = \"{config.PasswordAdmin}\";");
            sb.AppendLine();
            sb.AppendLine($"description = \"{config.Description}\";");
            sb.AppendLine();
            sb.AppendLine($"enableWhitelist = {(config.EnableWhitelist ? 1 : 0)};");
            sb.AppendLine();
            sb.AppendLine($"maxPlayers = {config.MaxPlayers};");
            sb.AppendLine();
            sb.AppendLine($"verifySignatures = {config.VerifySignatures};");
            sb.AppendLine($"forceSameBuild = {(config.ForceSameBuild ? 1 : 0)};");
            sb.AppendLine();
            sb.AppendLine($"disableVoN = {(config.DisableVoN ? 1 : 0)};");
            sb.AppendLine($"vonCodecQuality = {config.VonCodecQuality};");
            sb.AppendLine();
            sb.AppendLine($"shardId = \"{config.ShardId}\";");
            sb.AppendLine();
            sb.AppendLine($"disable3rdPerson = {(config.Disable3rdPerson ? 1 : 0)};");
            sb.AppendLine($"disableCrosshair = {(config.DisableCrosshair ? 1 : 0)};");
            sb.AppendLine();
            sb.AppendLine($"disablePersonalLight = {(config.DisablePersonalLight ? 1 : 0)};");
            sb.AppendLine($"lightingConfig = {config.LightingConfig};");
            sb.AppendLine();
            sb.AppendLine($"serverTime = \"{config.ServerTime}\";");
            sb.AppendLine($"serverTimeAcceleration = {config.ServerTimeAcceleration};");
            sb.AppendLine($"serverNightTimeAcceleration = {config.ServerNightTimeAcceleration};");
            sb.AppendLine($"serverTimePersistent = {(config.ServerTimePersistent ? 1 : 0)};");
            sb.AppendLine();
            sb.AppendLine($"guaranteedUpdates = 1;");
            sb.AppendLine();
            sb.AppendLine($"loginQueueConcurrentPlayers = {config.LoginQueueConcurrentPlayers};");
            sb.AppendLine($"loginQueueMaxPlayers = {config.LoginQueueMaxPlayers};");
            sb.AppendLine();
            sb.AppendLine($"instanceId = {config.InstanceId};");
            sb.AppendLine();
            sb.AppendLine($"storageAutoFix = {(config.StorageAutoFix ? 1 : 0)};");
            sb.AppendLine();
            sb.AppendLine($"RConPassword = \"{config.RConPassword}\";");
            sb.AppendLine();
            sb.AppendLine("class Missions");
            sb.AppendLine("{");
            sb.AppendLine("    class DayZ");
            sb.AppendLine("    {");
            sb.AppendLine($"        template = \"{config.MissionTemplate}\";");
            sb.AppendLine("    };");
            sb.AppendLine("};");
            File.WriteAllText(path, sb.ToString());
        }

        private void WriteBat(string path, ServerConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine(":start");
            sb.AppendLine("::Server name");
            sb.AppendLine($"set serverName={config.BatchServerName}");
            sb.AppendLine("::Server files location");
            sb.AppendLine($"set serverLocation=\"{config.ServerLocation}\"");
            sb.AppendLine("::Server Port");
            sb.AppendLine($"set serverPort={config.ServerPort}");
            sb.AppendLine("::Server config");
            sb.AppendLine($"set serverConfig={config.ConfigFileName}");
            sb.AppendLine("::Logical CPU cores to use (Equal or less than available)");
            sb.AppendLine($"set serverCPU={config.CpuCores}");
            sb.AppendLine("::Sets title for terminal (DONT edit)");
            sb.AppendLine("title %serverName% batch");
            sb.AppendLine("::DayZServer location (DONT edit)");
            sb.AppendLine("cd \"%serverLocation%\"");
            sb.AppendLine("echo (%time%) %serverName% started.");
            sb.AppendLine("::Launch parameters");

            var modParam = string.IsNullOrEmpty(config.ModList) ? "" : $"\"-mod={config.ModList}\" ";
            var srvModParam = string.IsNullOrEmpty(config.ServerModList) ? "" : $"\"-servermod={config.ServerModList}\" ";

            sb.Append($"start \"DayZ Server\" /min \"DayZServer_x64.exe\" -config=%serverConfig% -port=%serverPort% -cpuCount=%serverCPU% -dologs -adminlog -netlog -freezecheck \"-BEpath=%serverLocation%\\battleye\" \"-profiles=%serverLocation%\\profiles\" {modParam}{srvModParam}");
            sb.AppendLine();
            sb.AppendLine("::Time in seconds before kill server process");
            sb.AppendLine($"timeout {config.RestartInterval}");
            sb.AppendLine("taskkill /im DayZServer_x64.exe /F");
            sb.AppendLine("::Time in seconds to wait before..");
            sb.AppendLine("timeout 10");
            sb.AppendLine("::Go back to the top and repeat the whole cycle again");
            sb.AppendLine("goto start");
            File.WriteAllText(path, sb.ToString());
        }

        public void ExportConfig(ServerConfig config, string filePath)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public ServerConfig? ImportConfig(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ServerConfig>(json);
        }
    }
}
