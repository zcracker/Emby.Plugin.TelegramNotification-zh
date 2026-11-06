using System;
using System.Collections.Generic;
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
    /// configuration is used as a fallback.
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

            var imageInfo = request.GetSeriesImageInfo(ImageType.Primary)
                ?? request.GetSeriesImageInfo(ImageType.Thumb)
                ?? request.GetImageInfo(ImageType.Primary);

            // Poster branch: send the image with the title/description as caption.
            if (imageInfo != null && disablePoster != "true")
            {
                var caption = BuildMessage(request, disableDescription);
                var originalImageInfo = await imageInfo.GetImageInfo(cancellationToken).ConfigureAwait(false);
                await _fileSystem.ReadAllBytesAsync(originalImageInfo.Path, cancellationToken).ConfigureAwait(false);

                var requestUri = "https://api.telegram.org/bot" + botToken + "/sendPhoto";

                using (var httpClient = new HttpClient())
                using (var form = new MultipartFormDataContent())
                using (var fileStream = new FileStream(originalImageInfo.Path, FileMode.Open, FileAccess.Read))
                {
                    var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    form.Add(streamContent, "photo", Path.GetFileName(originalImageInfo.Path));
                    form.Add(new StringContent(chatID), "chat_id");
                    form.Add(new StringContent(caption), "caption");

                    var response = await httpClient.PostAsync(requestUri, form);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Telegram API error: {response.StatusCode} - {body}");
                    }
                }

                return;
            }

            // Text branch, split into 4096 character chunks (Telegram limit).
            var message = BuildMessage(request, disableDescription);

            if (message.Length > 4096)
            {
                var chunkSize = 4096;
                var messageLength = message.Length;

                for (var i = 0; i < messageLength; i += chunkSize)
                {
                    if (i + chunkSize > messageLength)
                    {
                        chunkSize = messageLength - i;
                    }

                    var text = Uri.EscapeDataString(message.Substring(i, chunkSize));
                    var httpRequestOptions = new HttpRequestOptions
                    {
                        Url = "https://api.telegram.org/bot" + botToken + "/sendmessage?chat_id=" + chatID + "&text=" + text,
                        CancellationToken = cancellationToken
                    };

                    using (await _httpClient.Post(httpRequestOptions).ConfigureAwait(false))
                    {
                    }
                }
            }
            else
            {
                var text = Uri.EscapeDataString(message);
                var httpRequestOptions = new HttpRequestOptions
                {
                    Url = "https://api.telegram.org/bot" + botToken + "/sendmessage?chat_id=" + chatID + "&text=" + text,
                    CancellationToken = cancellationToken
                };

                using (await _httpClient.Post(httpRequestOptions).ConfigureAwait(false))
                {
                }
            }
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
