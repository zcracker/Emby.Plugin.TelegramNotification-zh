using System;
using System.Collections.Generic;
using System.IO;
using Emby.Plugin.TelegramNotification.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;

namespace Emby.Plugin.TelegramNotification
{
    /// <summary>
    /// Telegram Notifications plugin.
    /// Adds a server-level configuration page (Bot Token / Chat ID / poster switch),
    /// a test button and a shortcut to Emby's own per-user notification entry editor.
    /// </summary>
    public class Plugin : BasePlugin, IHasWebPages, IHasThumbImage, IHasPluginConfiguration
    {
        private const string EditorJsName = "telegramnotificationeditorjs";
        private const string SettingsPageName = "telegram";
        private const string SettingsJsPageName = "telegramjs";

        private readonly Guid _id = new Guid("890ACB04-34A2-4CDB-8D89-4EA2FE90B0D7");

        public static string StaticName = "Telegram";

        public static Plugin Instance { get; private set; }

        private PluginConfiguration _configuration;
        private bool _configurationAssigned;

        /// <summary>
        /// Module used by Emby when the user opens the per-user notification entry editor.
        /// </summary>
        public string NotificationSetupModuleUrl => BasePlugin.GetPluginPageUrl(EditorJsName);

        public override Guid Id => _id;

        public override string Name => StaticName + " Notifications";

        public override string Description => "Sends notifications via Telegram Service.";

        /// <summary>
        /// Server-level configuration. Emby can hand over an empty object on startup,
        /// so the values are kept in an INI file next to the plugin data as well.
        /// </summary>
        public BasePluginConfiguration Configuration
        {
            get { return _configuration ?? (_configuration = LoadConfiguration()); }
            set
            {
                if (!(value is PluginConfiguration configuration))
                {
                    return;
                }

                if (!_configurationAssigned)
                {
                    _configurationAssigned = true;

                    // Keep what is on disk when Emby passes an empty object before the real save.
                    if (IsEmpty(configuration) && File.Exists(ConfigurationFilePath()))
                    {
                        _configuration = LoadConfiguration();
                        return;
                    }
                }

                _configuration = configuration;
                SaveConfigurationToDisk(configuration);
            }
        }

        public Type ConfigurationType => typeof(PluginConfiguration);

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public Plugin()
        {
            Instance = this;
            Configuration = new PluginConfiguration();
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = SettingsPageName,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html",
                    IsMainConfigPage = true,
                    EnableInMainMenu = true,
                    DisplayName = "Telegram 通知",
                    MenuIcon = "send"
                },
                new PluginPageInfo
                {
                    Name = SettingsJsPageName,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.js",
                    IsMainConfigPage = false
                },
                new PluginPageInfo
                {
                    Name = EditorJsName,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.entryeditor.js"
                },
                new PluginPageInfo
                {
                    Name = "telegrameditortemplate",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.entryeditor.template.html",
                    IsMainConfigPage = false
                }
            };
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        private static bool IsEmpty(PluginConfiguration configuration)
        {
            if (configuration == null)
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(configuration.BotToken)
                && string.IsNullOrWhiteSpace(configuration.ChatID)
                && !configuration.DisablePoster
                && !configuration.DisableDescription;
        }

        public void SetStartupInfo(Action<string> startupInfo)
        {
        }

        public void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            _configuration = configuration as PluginConfiguration ?? new PluginConfiguration();
            SaveConfigurationToDisk(_configuration);
        }

        private static string ConfigurationFilePath()
        {
            string folder = null;

            // Prefer the -programdata argument when Emby was started with one.
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-programdata", StringComparison.OrdinalIgnoreCase))
                {
                    folder = Path.Combine(args[i + 1], "plugins", "configurations");
                    break;
                }
            }

            if (string.IsNullOrEmpty(folder))
            {
                var dataFolder = Instance == null ? null : Instance.DataFolderPath;
                if (!string.IsNullOrEmpty(dataFolder))
                {
                    folder = Path.Combine(dataFolder, "configurations");
                }
            }

            if (string.IsNullOrEmpty(folder))
            {
                folder = Path.Combine(".", "configurations");
            }

            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            catch
            {
                // Best effort only - a failed write must not break the plugin.
            }

            return Path.Combine(folder, "TelegramNotification.ini");
        }

        private static PluginConfiguration LoadConfiguration()
        {
            var configuration = new PluginConfiguration();

            try
            {
                var path = ConfigurationFilePath();
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var separator = line.IndexOf('=');
                        if (separator <= 0)
                        {
                            continue;
                        }

                        var key = line.Substring(0, separator).Trim();
                        var value = line.Substring(separator + 1).Trim();

                        if (string.Equals(key, "BotToken", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.BotToken = value;
                        }
                        else if (string.Equals(key, "ChatID", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.ChatID = value;
                        }
                        else if (string.Equals(key, "DisablePoster", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.DisablePoster = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (string.Equals(key, "DisableDescription", StringComparison.OrdinalIgnoreCase))
                        {
                            configuration.DisableDescription = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                // Corrupt or unreadable file: fall back to defaults.
            }

            return configuration;
        }

        private static void SaveConfigurationToDisk(PluginConfiguration configuration)
        {
            try
            {
                File.WriteAllLines(ConfigurationFilePath(), new[]
                {
                    "# Emby.Plugin.TelegramNotification settings, last write " + DateTime.Now.ToString("s"),
                    "BotToken=" + (configuration.BotToken ?? string.Empty),
                    "ChatID=" + (configuration.ChatID ?? string.Empty),
                    "DisablePoster=" + (configuration.DisablePoster ? "true" : "false"),
                    "DisableDescription=" + (configuration.DisableDescription ? "true" : "false")
                });
            }
            catch
            {
                // Best effort only.
            }
        }
    }
}
