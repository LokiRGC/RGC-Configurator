using System.Collections.Generic;

namespace RGC.Services
{
    public static class Localization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
        {
            ["RU"] = new()
            {
                // Settings window
                ["settings.title"] = "Настройки приложения",
                ["settings.general"] = "Общие",
                ["settings.confirm_exit"] = "Подтверждать выход из приложения",
                ["settings.minimize_tray"] = "Сворачивать в трей вместо закрытия",
                ["settings.language"] = "Язык интерфейса",
                ["settings.notifications"] = "Уведомления",
                ["settings.notif_duration"] = "Длительность показа",
                ["settings.notif_sound"] = "Звук уведомлений",
                ["settings.sec"] = "сек",
                ["settings.appearance"] = "Оформление",
                ["settings.theme"] = "Тема",
                ["settings.dark"] = "Тёмная",
                ["settings.light"] = "Светлая",
                ["settings.autostart"] = "Открывать при загрузке Windows",
                ["settings.close"] = "✕ Закрыть",

                // Exit / Tray
                ["exit.title"] = "Выход",
                ["exit.message"] = "Вы действительно хотите выйти?",
                ["exit.yes"] = "Выйти",
                ["exit.no"] = "Отмена",
                ["tray.show"] = "Показать RGC",
                ["tray.exit"] = "Выйти",

                // Notifications
                ["notif.download_started"] = "Скачивание DayZ сервера началось",
                ["notif.mods_done"] = "Все моды скачаны",
                ["notif.no_mods"] = "Нет модов для скачивания",
                ["notif.internet_ok"] = "Интернет подключён",
                ["notif.internet_fail"] = "Нет доступа к интернету",

                // Tabs
                ["tab.main"] = "Основные",
                ["tab.world"] = "Мир",
                ["tab.network"] = "Сеть",
                ["tab.mods"] = "Моды",
                ["tab.batch"] = "Batch",
                ["tab.modconfig"] = "Config модов",
                ["tab.launch"] = "Запуск",
                ["tab.rcon"] = "RCon",
                ["tab.stats"] = "Статистика",

                // Launch tab
                ["launch.start"] = "▶ Запустить",
                ["launch.stop"] = "■ Остановить",
                ["launch.clear"] = "Очистить лог",
                ["launch.install"] = "⬇ Установить сервер",
                ["launch.check_version"] = "🔍 Версия",
                ["launch.path"] = "Папка сервера",
                ["launch.browse"] = "Обзор",

                // Config
                ["config.load"] = "Загрузить конфиг",
                ["config.save"] = "Сохранить",
                ["config.scan_mods"] = "Сканировать моды",
                ["config.export"] = "📤 Экспорт",
                ["config.import"] = "📥 Импорт",

                // RCon tab
                ["rcon.connect"] = "Подключиться",
                ["rcon.disconnect"] = "Отключиться",
                ["rcon.send"] = "Отправить",
                ["rcon.host"] = "Хост",
                ["rcon.port"] = "Порт",
                ["rcon.password"] = "Пароль",

                // Stats tab
                ["stats.title"] = "Статистика игроков",
                ["stats.player"] = "Игрок",
                ["stats.playtime"] = "Время игры",
                ["stats.last_seen"] = "Последний визит",
                ["stats.status"] = "Статус",
                ["stats.online"] = "Онлайн",
                ["stats.offline"] = "Офлайн",

                // Library
                ["lib.title"] = "Мои проекты",
                ["lib.new"] = "＋ Новый проект",
                ["lib.count"] = "{0} проект|{0} проекта|{0} проектов",
                ["lib.empty_title"] = "Нет проектов",
                ["lib.empty_desc"] = "Создайте первый проект, чтобы управлять серверами",
                ["lib.empty_btn"] = "＋ Создать проект",
                ["lib.not_configured"] = "Сервер не настроен",
                ["lib.configured"] = "Сервер настроен",
                ["lib.no_path"] = "путь не указан",
                ["lib.open"] = "▶ Открыть",
                ["lib.created"] = "Создан {0}",
                ["lib.last_open"] = "открыт {0}",
                ["lib.delete_title"] = "Удаление проекта",
                ["lib.delete_msg"] = "Удалить проект «{0}»?\nФайлы сервера останутся на месте.",
                ["lib.new_title"] = "Новый проект",
                ["lib.new_name"] = "Название проекта",
                ["lib.new_btn"] = "Создать проект",
                ["lib.new_cancel"] = "Отмена",
                ["lib.new_name_err"] = "Введите название проекта.",
                ["lib.server_path"] = "путь не указан",
                ["lib.dayz_version"] = "DayZ {0}",
                ["lib.no_version"] = "Версия неизвестна",
                ["lib.gen_tooltip"] = "Сгенерировать название",

                // Server config sections
                ["cfg.main_title"] = "Основные настройки сервера",
                ["cfg.hostname"] = "Название сервера",
                ["cfg.password"] = "Пароль для входа",
                ["cfg.admin_password"] = "Пароль администратора",
                ["cfg.max_players"] = "Максимум игроков",
                ["cfg.description"] = "Описание сервера",
                ["cfg.shard_id"] = "Shard ID (6 символов)",
                ["cfg.mission"] = "Миссия (template)",
                ["cfg.whitelist"] = "Включить whitelist",

                // World section
                ["world.title"] = "Игровой процесс и мир",
                ["world.3rd_person"] = "Отключить вид от 3-го лица",
                ["world.crosshair"] = "Отключить прицел",
                ["world.personal_light"] = "Отключить личный фонарик",
                ["world.lighting"] = "Освещение (0 - яркая ночь, 1 - тёмная)",
                ["world.server_time"] = "Начальное время (SystemTime или YYYY/MM/DD/HH/MM)",
                ["world.time_accel"] = "Ускорение времени (0-24)",
                ["world.night_accel"] = "Ускорение ночи (0.1-64)",
                ["world.time_persist"] = "Сохранять время",

                // Network section
                ["network.title"] = "Сетевые настройки",
                ["network.port"] = "Порт сервера",
                ["network.verify_sig"] = "Проверка подписей (2)",
                ["network.force_build"] = "Требовать ту же сборку",
                ["network.disable_von"] = "Отключить голосовой чат",
                ["network.von_quality"] = "Качество голоса (0-30)",
                ["network.login_concurrent"] = "Одновременных входов",
                ["network.login_max"] = "Макс. очередь входа",
                ["network.instance_id"] = "Instance ID",
                ["network.rcon_pass"] = "RCon пароль",

                // Mods tab
                ["mods.client"] = "Моды клиента",
                ["mods.server"] = "Серверные моды",
                ["mods.add"] = "Добавить",
                ["mods.remove"] = "Удалить",
                ["mods.open_folder"] = "Открыть папку",
                ["mods.workshop"] = "Steam Workshop",

                // Batch tab
                ["batch.title"] = "Настройки Batch",
                ["batch.server_name"] = "Название сервера",
                ["batch.port"] = "Порт в batch",
                ["batch.cpu"] = "Ядер CPU",
                ["batch.config"] = "Имя конфига",
                ["batch.restart"] = "Интервал рестарта (мин)",
                ["batch.restart_disabled"] = "0 - отключён",

                // Mod config tab
                ["modconfig.title"] = "Файлы конфигурации модов",

                // Library sections
                ["lib.local_title"] = "Локальные проекты",
                ["lib.ftp_title"] = "FTP проекты",
                ["lib.tools_title"] = "Инструменты",
                ["lib.add_ftp"] = "＋",
                ["lib.new_desc"] = "Папку сервера можно будет указать позже в настройках проекта.",

                // FTP window
                ["ftp.settings"] = "Настройки",
                ["ftp.title_new"] = "Новый FTP проект",
                ["ftp.title_edit"] = "Настройки FTP проекта",
                ["ftp.name"] = "Название",
                ["ftp.host"] = "Хост",
                ["ftp.port"] = "Порт",
                ["ftp.username"] = "Имя пользователя",
                ["ftp.password"] = "Пароль",
                ["ftp.remote_path"] = "Удалённый путь",
                ["ftp.connect_label"] = "Подключение к FTP",
                ["ftp.connect_btn"] = "🔗 Подключиться к FTP",
                ["ftp.local_path"] = "Локальный путь",
                ["ftp.save"] = "💾 Сохранить",
                ["ftp.create"] = "➕ Создать",
                ["ftp.cancel"] = "Отмена",
                ["ftp.delete"] = "🗑 Удалить",
                ["ftp.fill_required"] = "Заполните название и хост.",
                ["ftp.delete_confirm"] = "Удалить FTP проект «{0}»?",

                // Tools
                ["tools.steamcmd"] = "Установка и обновление DayZ сервера через SteamCMD",
                ["tools.configconv"] = "Конвертация конфигов между форматами",
                ["tools.run"] = "Запуск",

                // Progress
                ["progress.download"] = "Скачивание...",
                ["progress.installing"] = "Установка...",

                // Startup choice
                ["startup.title"] = "RGC DayZ Configurator",
                ["startup.ask"] = "Что вы хотите сделать?",
                ["startup.edit_server"] = "Редактировать сервер",
                ["startup.edit_server_desc"] = "Открыть существующий проект или создать новый",
                ["startup.connect_ftp"] = "Подключиться к FTP",
                ["startup.connect_ftp_desc"] = "Подключиться к FTP серверу для управления файлами",
                ["startup.autobuild"] = "Автосборка проекта",
                ["startup.autobuild_desc"] = "Собрать сервер с модами и конфигами автоматически",
                ["startup.autobuild_soon"] = "Скоро будет доступно!",
            },
            ["EN"] = new()
            {
                // Settings window
                ["settings.title"] = "Application Settings",
                ["settings.general"] = "General",
                ["settings.confirm_exit"] = "Confirm on exit",
                ["settings.minimize_tray"] = "Minimize to tray instead of closing",
                ["settings.language"] = "Interface language",
                ["settings.notifications"] = "Notifications",
                ["settings.notif_duration"] = "Display duration",
                ["settings.notif_sound"] = "Notification sound",
                ["settings.sec"] = "sec",
                ["settings.appearance"] = "Appearance",
                ["settings.theme"] = "Theme",
                ["settings.dark"] = "Dark",
                ["settings.light"] = "Light",
                ["settings.autostart"] = "Launch at Windows startup",
                ["settings.close"] = "✕ Close",

                // Exit / Tray
                ["exit.title"] = "Exit",
                ["exit.message"] = "Are you sure you want to exit?",
                ["exit.yes"] = "Exit",
                ["exit.no"] = "Cancel",
                ["tray.show"] = "Show RGC",
                ["tray.exit"] = "Exit",

                // Notifications
                ["notif.download_started"] = "DayZ server download started",
                ["notif.mods_done"] = "All mods downloaded",
                ["notif.no_mods"] = "No mods to download",
                ["notif.internet_ok"] = "Internet connected",
                ["notif.internet_fail"] = "No internet access",

                // Tabs
                ["tab.main"] = "Main",
                ["tab.world"] = "World",
                ["tab.network"] = "Network",
                ["tab.mods"] = "Mods",
                ["tab.batch"] = "Batch",
                ["tab.modconfig"] = "Mod Config",
                ["tab.launch"] = "Launch",
                ["tab.rcon"] = "RCon",
                ["tab.stats"] = "Stats",

                // Launch tab
                ["launch.start"] = "▶ Start",
                ["launch.stop"] = "■ Stop",
                ["launch.clear"] = "Clear log",
                ["launch.install"] = "⬇ Install Server",
                ["launch.check_version"] = "🔍 Version",
                ["launch.path"] = "Server folder",
                ["launch.browse"] = "Browse",

                // Config
                ["config.load"] = "Load config",
                ["config.save"] = "Save",
                ["config.scan_mods"] = "Scan mods",
                ["config.export"] = "📤 Export",
                ["config.import"] = "📥 Import",

                // RCon tab
                ["rcon.connect"] = "Connect",
                ["rcon.disconnect"] = "Disconnect",
                ["rcon.send"] = "Send",
                ["rcon.host"] = "Host",
                ["rcon.port"] = "Port",
                ["rcon.password"] = "Password",

                // Stats tab
                ["stats.title"] = "Player Statistics",
                ["stats.player"] = "Player",
                ["stats.playtime"] = "Playtime",
                ["stats.last_seen"] = "Last seen",
                ["stats.status"] = "Status",
                ["stats.online"] = "Online",
                ["stats.offline"] = "Offline",

                // Library
                ["lib.title"] = "My Projects",
                ["lib.new"] = "＋ New Project",
                ["lib.count"] = "{0} project|{0} projects|{0} projects",
                ["lib.empty_title"] = "No projects",
                ["lib.empty_desc"] = "Create your first project to manage servers",
                ["lib.empty_btn"] = "＋ Create project",
                ["lib.not_configured"] = "Server not configured",
                ["lib.configured"] = "Server configured",
                ["lib.no_path"] = "no path set",
                ["lib.open"] = "▶ Open",
                ["lib.created"] = "Created {0}",
                ["lib.last_open"] = "opened {0}",
                ["lib.delete_title"] = "Delete project",
                ["lib.delete_msg"] = "Delete project «{0}»?\nServer files will remain.",
                ["lib.new_title"] = "New Project",
                ["lib.new_name"] = "Project name",
                ["lib.new_btn"] = "Create project",
                ["lib.new_cancel"] = "Cancel",
                ["lib.new_name_err"] = "Enter a project name.",
                ["lib.server_path"] = "no path set",
                ["lib.dayz_version"] = "DayZ {0}",
                ["lib.no_version"] = "Unknown version",
                ["lib.gen_tooltip"] = "Generate name",

                // Server config sections
                ["cfg.main_title"] = "Main Server Settings",
                ["cfg.hostname"] = "Server name",
                ["cfg.password"] = "Password",
                ["cfg.admin_password"] = "Admin password",
                ["cfg.max_players"] = "Max players",
                ["cfg.description"] = "Description",
                ["cfg.shard_id"] = "Shard ID (6 chars)",
                ["cfg.mission"] = "Mission template",
                ["cfg.whitelist"] = "Enable whitelist",

                // World section
                ["world.title"] = "Gameplay & World",
                ["world.3rd_person"] = "Disable 3rd person view",
                ["world.crosshair"] = "Disable crosshair",
                ["world.personal_light"] = "Disable personal light",
                ["world.lighting"] = "Lighting (0 - bright night, 1 - dark)",
                ["world.server_time"] = "Start time (SystemTime or YYYY/MM/DD/HH/MM)",
                ["world.time_accel"] = "Time acceleration (0-24)",
                ["world.night_accel"] = "Night acceleration (0.1-64)",
                ["world.time_persist"] = "Persistent time",

                // Network section
                ["network.title"] = "Network Settings",
                ["network.port"] = "Server port",
                ["network.verify_sig"] = "Verify signatures (2)",
                ["network.force_build"] = "Force same build",
                ["network.disable_von"] = "Disable voice chat",
                ["network.von_quality"] = "Voice quality (0-30)",
                ["network.login_concurrent"] = "Concurrent logins",
                ["network.login_max"] = "Max login queue",
                ["network.instance_id"] = "Instance ID",
                ["network.rcon_pass"] = "RCon password",

                // Mods tab
                ["mods.client"] = "Client mods",
                ["mods.server"] = "Server mods",
                ["mods.add"] = "Add",
                ["mods.remove"] = "Remove",
                ["mods.open_folder"] = "Open folder",
                ["mods.workshop"] = "Steam Workshop",

                // Batch tab
                ["batch.title"] = "Batch Settings",
                ["batch.server_name"] = "Server name",
                ["batch.port"] = "Batch port",
                ["batch.cpu"] = "CPU cores",
                ["batch.config"] = "Config filename",
                ["batch.restart"] = "Restart interval (min)",
                ["batch.restart_disabled"] = "0 - disabled",

                // Mod config tab
                ["modconfig.title"] = "Mod Configuration Files",

                // Library sections
                ["lib.local_title"] = "Local Projects",
                ["lib.ftp_title"] = "FTP Projects",
                ["lib.tools_title"] = "Tools",
                ["lib.add_ftp"] = "＋",
                ["lib.new_desc"] = "You can set the server folder later in project settings.",

                // FTP window
                ["ftp.settings"] = "Settings",
                ["ftp.title_new"] = "New FTP Project",
                ["ftp.title_edit"] = "FTP Project Settings",
                ["ftp.name"] = "Name",
                ["ftp.host"] = "Host",
                ["ftp.port"] = "Port",
                ["ftp.username"] = "Username",
                ["ftp.password"] = "Password",
                ["ftp.remote_path"] = "Remote Path",
                ["ftp.connect_label"] = "FTP Connection",
                ["ftp.connect_btn"] = "🔗 Connect to FTP",
                ["ftp.local_path"] = "Local Path",
                ["ftp.save"] = "💾 Save",
                ["ftp.create"] = "➕ Create",
                ["ftp.cancel"] = "Cancel",
                ["ftp.delete"] = "🗑 Delete",
                ["ftp.fill_required"] = "Please fill in Name and Host.",
                ["ftp.delete_confirm"] = "Delete FTP project «{0}»?",

                // Tools
                ["tools.steamcmd"] = "Install and update DayZ server via SteamCMD",
                ["tools.configconv"] = "Convert configs between formats",
                ["tools.run"] = "Run",

                // Progress
                ["progress.download"] = "Downloading...",
                ["progress.installing"] = "Installing...",

                // Startup choice
                ["startup.title"] = "RGC DayZ Configurator",
                ["startup.ask"] = "What would you like to do?",
                ["startup.edit_server"] = "Edit Server",
                ["startup.edit_server_desc"] = "Open an existing project or create a new one",
                ["startup.connect_ftp"] = "Connect to FTP",
                ["startup.connect_ftp_desc"] = "Connect to an FTP server to manage files",
                ["startup.autobuild"] = "Auto-build Project",
                ["startup.autobuild_desc"] = "Automatically assemble a server with mods and configs",
                ["startup.autobuild_soon"] = "Coming soon!",
            }
        };

        public static string Get(string key)
        {
            var lang = SettingsService.Language;
            if (_strings.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
                return val;
            // Fallback to RU
            if (_strings.TryGetValue("RU", out var ru) && ru.TryGetValue(key, out var ruVal))
                return ruVal;
            return $"?{key}?";
        }

        public static string T(string key) => Get(key);
    }
}
