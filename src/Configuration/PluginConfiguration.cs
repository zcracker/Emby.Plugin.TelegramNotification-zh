using MediaBrowser.Model.Plugins;

namespace Emby.Plugin.TelegramNotification.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
	public string BotToken { get; set; }

	public string ChatID { get; set; }

	public bool DisablePoster { get; set; }

	public bool DisableDescription { get; set; }

	public PluginConfiguration()
	{
		BotToken = string.Empty;
		ChatID = string.Empty;
	}
}
