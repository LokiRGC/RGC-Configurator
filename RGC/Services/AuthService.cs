using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RGC.Services
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class UserCredentials
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
    }

    public class AuthService
    {
        private readonly string _dataDir;
        private readonly string _authFile;
        private readonly string _rememberFile;

        public AuthService()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC");
            Directory.CreateDirectory(_dataDir);
            _authFile = Path.Combine(_dataDir, "auth.dat");
            _rememberFile = Path.Combine(_dataDir, "remember.dat");
        }

        public bool IsRegistered()
        {
            return File.Exists(_authFile);
        }

        public AuthResult Register(string username, string password)
        {
            if (IsRegistered())
                return new AuthResult { Success = false, Message = "Пользователь уже зарегистрирован." };

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return new AuthResult { Success = false, Message = "Имя и пароль не могут быть пустыми." };

            var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var hash = HashPassword(password, salt);

            var creds = new UserCredentials
            {
                Username = username,
                PasswordHash = hash,
                Salt = salt
            };

            var json = JsonSerializer.Serialize(creds);
            File.WriteAllText(_authFile, json);

            return new AuthResult { Success = true, Message = "Регистрация успешна!" };
        }

        public AuthResult Login(string username, string password)
        {
            if (!IsRegistered())
                return new AuthResult { Success = false, Message = "Нет зарегистрированных пользователей." };

            var json = File.ReadAllText(_authFile);
            var creds = JsonSerializer.Deserialize<UserCredentials>(json);

            if (creds == null)
                return new AuthResult { Success = false, Message = "Ошибка чтения данных." };

            if (creds.Username != username)
                return new AuthResult { Success = false, Message = "Неверное имя пользователя." };

            var hash = HashPassword(password, creds.Salt);
            if (hash != creds.PasswordHash)
                return new AuthResult { Success = false, Message = "Неверный пароль." };

            return new AuthResult { Success = true, Message = "Вход выполнен!" };
        }

        public void SaveRemembered(string username)
        {
            File.WriteAllText(_rememberFile, username);
        }

        public void ClearRemembered()
        {
            if (File.Exists(_rememberFile))
                File.Delete(_rememberFile);
        }

        public string? GetRemembered()
        {
            return File.Exists(_rememberFile) ? File.ReadAllText(_rememberFile) : null;
        }

        private static string HashPassword(string password, string salt)
        {
            var bytes = Encoding.UTF8.GetBytes(password + salt);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
