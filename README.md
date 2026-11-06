# Emby Telegram Notifications（中文增强版）

Emby 服务器插件：把播放/入库等通知推送到 Telegram Bot。本仓库是上游
[bjoerns1983/Emby.Plugin.TelegramNotification](https://github.com/bjoerns1983/Emby.Plugin.TelegramNotification)
（GPL-2.0）的修改版，当前版本 `1.4.8.0`，界面中文化并补上了服务器级配置与代理支持。

## 与上游的差异

- **服务器级配置页**（Emby 4.10 插件页，左侧菜单「Telegram 通知」）——所有 Telegram 数据只在这一处填
  - 名称 / Bot Token / Chat ID / 停用海报 / 停用剧情简介 / 代理
  - 「获取 Chat-ID」：直接读 Bot 的 `getUpdates` 自动填 Chat ID
  - 「发送测试消息」：不用等真实播放事件就能验证配置
  - 「打开完整通知编辑器」：打开 Emby 单条通知条目，那里只勾事件（订阅 / 用户 / 媒体库 / 设备），不再重复填 Token
- **配置回落**：单条通知条目里留空的 Bot Token / Chat ID / 开关，自动回落到服务器级配置；插件页保存时会把同样的值写回条目，两处不会不一致
- **海报通知**：有海报时走 `sendPhoto`（标题 + 简介作为 caption），可用开关关掉海报或简介
- **长文本分片**：超过 Telegram 4096 字符上限时按 4096 自动拆分成多条
- **配置持久化**：除 Emby 自身的插件配置外，同时写入
  `plugins/configurations/TelegramNotification.ini`，避免重启后配置被空对象覆盖
- **代理支持**：直连、HTTP、HTTPS、SOCKS5，可带用户名密码。
  HTTP/HTTPS 代理访问 `https://api.telegram.org` 时走 `CONNECT` 隧道，海报和文字消息都走同一个代理

## 安装

1. 从 [Releases](../../releases) 下载 `Emby.Plugin.TelegramNotification-1.4.8.0.dll`
2. 放进 Emby 的插件目录，例如 `/var/lib/emby/plugins`（Windows 一般是
   `C:\Users\<用户>\AppData\Roaming\Emby-Server\programdata\plugins`）
3. 重启 Emby
4. 后台 → 设置 → 插件 → 「Telegram 通知」，填 Bot Token 和 Chat ID（可点「获取 Chat-ID」）
5. 先点「发送测试消息」确认能收到，再去「打开完整通知编辑器」勾选要推送的事件（该弹窗只有事件选项，没有第二份 Telegram 表单）

## 编译

需要 .NET SDK 8 或更新（目标框架是 `netstandard2.0`）：

```bash
dotnet build -c Release
```

产物：`bin/Release/netstandard2.0/Emby.Plugin.TelegramNotification.dll`。

引用的是 NuGet 包 `mediabrowser.server.core 4.8.11`，对应 Emby Server 4.8.11 的
`MediaBrowser.Controller` / `MediaBrowser.Model` / `MediaBrowser.Common`。
本仓库 `dist/` 下的 DLL 就是用仓库内源码编译出来的。

## 代理

插件设置页「代理」一节：

- **不使用代理**：直连 `api.telegram.org`（国内服务器通常不通）
- **HTTP 代理**：`http://host:port`，例如 `10.0.0.115:7890`（Clash/Stash/mihomo 的混合端口）
- **HTTPS 代理**：先和代理本身建立 TLS，再 `CONNECT`（代理端口本身必须是 TLS 服务）
- **SOCKS5**：`socks5://host:port`

注意：

- SOCKS5 与 HTTPS 代理依赖运行时的实现，需要 .NET 6 及以上（Emby 4.8+ 自带）；旧运行时只支持 HTTP 代理
- 代理地址栏可以只填 `host`，也可以填 `host:port` 或 `http://host:port`，端口字段会作为兜底
- 用户名/密码可选，密码以明文保存在 Emby 的插件配置和 `TelegramNotification.ini` 里，注意目录权限
- 保存后点「发送测试消息」即可验证代理是否通；失败时 Emby 日志会写明 `通过代理 http://host:port 连接 Telegram 失败: ...`

## 目录

```
src/Plugin.cs             插件入口：配置页、页面注册、配置读写
src/Notifier.cs           通知发送：海报 / 文本 / 分片
src/ProxyHelper.cs        代理构造：http / https / socks5 + 认证
src/Configuration/        PluginConfiguration 与前端页面（html/js）
dist/                     编译好的 DLL
```

## 许可

GPL-2.0，见 [LICENSE](LICENSE)。原始版权归 bjoerns1983 及上游贡献者所有；
本仓库对其修改部分同样以 GPL-2.0 发布。
