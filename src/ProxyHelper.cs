using System;
using System.Net;
using System.Net.Http;
using Emby.Plugin.TelegramNotification.Configuration;

namespace Emby.Plugin.TelegramNotification
{
    /// <summary>
    /// Builds the HTTP client used to reach api.telegram.org, optionally through a
    /// HTTP / HTTPS / SOCKS5 proxy.
    /// </summary>
    internal static class ProxyHelper
    {
        public static bool IsEnabled(PluginConfiguration configuration)
        {
            return configuration != null && !string.IsNullOrWhiteSpace(configuration.ProxyType);
        }

        /// <summary>
        /// Creates the proxy for the current configuration, or null when no proxy is configured.
        /// </summary>
        public static IWebProxy CreateProxy(PluginConfiguration configuration)
        {
            if (!IsEnabled(configuration))
            {
                return null;
            }

            var type = (configuration.ProxyType ?? string.Empty).Trim().ToLowerInvariant();
            if (type != "http" && type != "https" && type != "socks5")
            {
                throw new InvalidOperationException("不支持的代理类型: " + configuration.ProxyType);
            }

            var host = (configuration.ProxyHost ?? string.Empty).Trim();
            if (host.Length == 0)
            {
                throw new InvalidOperationException("已选择代理类型，但没有填写代理地址");
            }

            // Allow the address field to contain "host:port" or a full URI.
            if (host.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                host = host.Substring(host.IndexOf("://", StringComparison.Ordinal) + 3);
            }

            var slash = host.IndexOf('/');
            if (slash >= 0)
            {
                host = host.Substring(0, slash);
            }

            var port = configuration.ProxyPort;
            var colon = host.LastIndexOf(':');
            if (colon > 0 && host.IndexOf(']') < colon)
            {
                // host:port was typed in the address field
                if (int.TryParse(host.Substring(colon + 1), out var inlinePort) && inlinePort > 0)
                {
                    port = inlinePort;
                }

                host = host.Substring(0, colon);
            }

            if (port <= 0 || port > 65535)
            {
                throw new InvalidOperationException("代理端口无效，请填写 1-65535 之间的端口");
            }

            var proxy = new WebProxy(string.Format("{0}://{1}:{2}", type, host, port));

            if (!string.IsNullOrWhiteSpace(configuration.ProxyUser))
            {
                proxy.Credentials = new NetworkCredential(
                    configuration.ProxyUser,
                    configuration.ProxyPass ?? string.Empty);
            }

            return proxy;
        }

        /// <summary>
        /// Creates an HttpClient that routes through the configured proxy (if any).
        /// </summary>
        public static HttpClient CreateClient(PluginConfiguration configuration)
        {
            var proxy = CreateProxy(configuration);

            var handler = new HttpClientHandler
            {
                UseProxy = proxy != null,
                UseCookies = false
            };

            if (proxy != null)
            {
                handler.Proxy = proxy;
            }

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }
    }
}
