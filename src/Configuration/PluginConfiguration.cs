using MediaBrowser.Model.Plugins;

namespace Emby.Plugin.TelegramNotification.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string BotToken { get; set; }

        public string ChatID { get; set; }

        public bool DisablePoster { get; set; }

        public bool DisableDescription { get; set; }

        /// <summary>空 = 不使用代理；http / https / socks5。</summary>
        public string ProxyType { get; set; }

        public string ProxyHost { get; set; }

        public int ProxyPort { get; set; }

        public string ProxyUser { get; set; }

        public string ProxyPass { get; set; }

        public PluginConfiguration()
        {
            BotToken = string.Empty;
            ChatID = string.Empty;
            ProxyType = string.Empty;
            ProxyHost = string.Empty;
            ProxyPort = 0;
            ProxyUser = string.Empty;
            ProxyPass = string.Empty;
        }
    }
}
