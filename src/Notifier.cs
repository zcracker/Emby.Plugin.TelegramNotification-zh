using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Emby.Notifications;
using Emby.Plugin.TelegramNotification.Configuration;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.TelegramNotification
{
    /// <summary>
    /// Emby user notifier that pushes a message (optionally with the poster) to a Telegram chat.
    /// Values set on the per-user notification entry win; the server-level plugin
    /// configuration is used as a fallback. All requests can be routed through a
    /// HTTP / HTTPS / SOCKS5 proxy configured in the plugin settings.
    /// </summary>
    public class Notifier : IUserNotifier
    {
        private readonly ILogger _logger;
        private readonly IServerApplicationHost _appHost;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IFileSystem _fileSystem;

        public Notifier(
            ILogger logger,
            IServerApplicationHost applicationHost,
            IHttpClient httpClient,
            IJsonSerializer jsonSerializer,
            IFileSystem fileSystem)
        {
            _logger = logger;
            _appHost = applicationHost;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
        }

        private Plugin Plugin => _appHost.Plugins.OfType<Plugin>().First();

        public string Name => Plugin.StaticName;

        public string Key => "telegramnotifications";

        public string SetupModuleUrl => Plugin.NotificationSetupModuleUrl;

        public async Task SendNotification(InternalNotificationRequest request, CancellationToken cancellationToken)
        {
            var options = request.Configuration.Options;

            options.TryGetValue("BotToken", out var botToken);
            options.TryGetValue("ChatID", out var chatID);
            options.TryGetValue("DisableDescription", out var disableDescription);
            options.TryGetValue("DisablePoster", out var disablePoster);

            if (Plugin.Configuration is PluginConfiguration pluginConfiguration)
            {
                if (string.IsNullOrWhiteSpace(botToken))
                {
                    botToken = pluginConfiguration.BotToken;
                }

                if (string.IsNullOrWhiteSpace(chatID))
                {
                    chatID = pluginConfiguration.ChatID;
                }

                if (string.IsNullOrWhiteSpace(disableDescription))
                {
                    disableDescription = pluginConfiguration.DisableDescription ? "true" : "false";
                }

                if (string.IsNullOrWhiteSpace(disablePoster))
                {
                    disablePoster = pluginConfiguration.DisablePoster ? "true" : "false";
                }
            }

            var configuration = Plugin.Configuration as PluginConfiguration;
            var message = BuildMessage(request, disableDescription);

            using (var httpClient = ProxyHelper.CreateClient(configuration))
            {
                try
                {
                    var imageInfo = request.GetSeriesImageInfo(ImageType.Primary)
                        ?? request.GetSeriesImageInfo(ImageType.Thumb)
                        ?? request.GetImageInfo(ImageType.Primary);

                    if (imageInfo != null && disablePoster != "true")
                    {
                        await SendPhoto(httpClient, imageInfo, botToken, chatID, message, cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    await SendText(httpClient, botToken, chatID, message, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (ProxyHelper.IsEnabled(configuration))
                {
                    throw new Exception(
                        "通过代理 " + DescribeProxy(configuration) + " 连接 Telegram 失败: " + ex.Message, ex);
                }
            }
        }

        private async Task SendPhoto(
            HttpClient httpClient,
            ApiImageInfo imageInfo,
            string botToken,
            string chatID,
            string caption,
            CancellationToken cancellationToken)
        {
            var originalImageInfo = await imageInfo.GetImageInfo(cancellationToken).ConfigureAwait(false);
            await _fileSystem.ReadAllBytesAsync(originalImageInfo.Path, cancellationToken).ConfigureAwait(false);

            var requestUri = "https://api.telegram.org/bot" + botToken + "/sendPhoto";

            using (var form = new MultipartFormDataContent())
            using (var fileStream = new FileStream(originalImageInfo.Path, FileMode.Open, FileAccess.Read))
            {
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(streamContent, "photo", Path.GetFileName(originalImageInfo.Path));
                form.Add(new StringContent(chatID), "chat_id");
                form.Add(new StringContent(caption), "caption");

                var response = await httpClient.PostAsync(requestUri, form, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Telegram API error: {response.StatusCode} - {body}");
                }
            }
        }

        private static async Task SendText(
            HttpClient httpClient,
            string botToken,
            string chatID,
            string message,
            CancellationToken cancellationToken)
        {
            // Telegram caps a single message at 4096 characters.
            const int chunkSize = 4096;

            if (message.Length <= chunkSize)
            {
                await PostText(httpClient, botToken, chatID, message, cancellationToken).ConfigureAwait(false);
                return;
            }

            for (var i = 0; i < message.Length; i += chunkSize)
            {
                var length = Math.Min(chunkSize, message.Length - i);
                await PostText(httpClient, botToken, chatID, message.Substring(i, length), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static async Task PostText(
            HttpClient httpClient,
            string botToken,
            string chatID,
            string text,
            CancellationToken cancellationToken)
        {
            var url = "https://api.telegram.org/bot" + botToken
                + "/sendmessage?chat_id=" + chatID
                + "&text=" + Uri.EscapeDataString(text);

            using (var content = new StringContent(string.Empty))
            using (var response = await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false))
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Telegram API error: {response.StatusCode} - {body}");
                }
            }
        }

        private static string DescribeProxy(PluginConfiguration configuration)
        {
            if (configuration == null)
            {
                return "(未配置)";
            }

            return string.Format("{0}://{1}:{2}", configuration.ProxyType, configuration.ProxyHost, configuration.ProxyPort);
        }

        private static string BuildMessage(InternalNotificationRequest request, string disableDescription)
        {
            if (!string.IsNullOrEmpty(request.Description) && disableDescription != "true")
            {
                return request.Title + "\n\n" + request.Description;
            }

            return request.Title;
        }
    }
}
