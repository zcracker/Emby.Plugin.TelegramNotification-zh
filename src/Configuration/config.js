define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-checkbox', 'pluginManager'], function (BaseView, loading, embyInput, embyButton, embyCheckbox, pluginManager) {
    'use strict';

    var pluginId = '890acb04-34a2-4cdb-8d89-4ea2fe90b0d7';
    var notifierKey = 'telegramnotifications';
    var currentEntry = null;

    function getConfig() {
        return ApiClient.getPluginConfiguration(pluginId);
    }

    function toast(text) {
        Emby.importModule('./modules/toast/toast.js').then(function (showToast) {
            showToast(text);
        }, function () {
        });
    }

    function getTelegramEntry() {
        var userId = ApiClient.getCurrentUserId();
        return ApiClient.getJSON(ApiClient.getUrl('Notifications/Services/Configured', { UserId: userId })).then(function (entries) {
            entries = entries || [];
            for (var i = 0; i < entries.length; i++) {
                if (entries[i].NotifierKey === notifierKey) {
                    return entries[i];
                }
            }
            return null;
        });
    }

    function loadPage(page, config) {

        page.querySelector('.txtTelegramBotToken').value = config.BotToken || '';
        page.querySelector('.txtTelegramChatID').value = config.ChatID || '';
        page.querySelector('.chkTelegramDisablePoster').checked = config.DisablePoster === true;
        page.querySelector('.chkTelegramDisableDescription').checked = config.DisableDescription === true;
        page.querySelector('.selProxyType').value = config.ProxyType || '';
        page.querySelector('.txtProxyHost').value = config.ProxyHost || '';
        page.querySelector('.txtProxyPort').value = config.ProxyPort || '';
        page.querySelector('.txtProxyUser').value = config.ProxyUser || '';
        page.querySelector('.txtProxyPass').value = config.ProxyPass || '';

        return getTelegramEntry().then(function (entry) {
            currentEntry = entry;
            page.querySelector('.txtFriendlyName').value = entry ? (entry.FriendlyName || '') : '';
        }, function () {
        });
    }

    function onSubmit(e) {

        e.preventDefault();
        loading.show();

        var form = this;
        var page = this;
        var friendlyName = form.querySelector('.txtFriendlyName').value;

        getConfig().then(function (config) {

            config.BotToken = form.querySelector('.txtTelegramBotToken').value;
            config.ChatID = form.querySelector('.txtTelegramChatID').value;
            config.DisablePoster = form.querySelector('.chkTelegramDisablePoster').checked;
            config.DisableDescription = form.querySelector('.chkTelegramDisableDescription').checked;
            config.ProxyType = form.querySelector('.selProxyType').value || '';
            config.ProxyHost = form.querySelector('.txtProxyHost').value || '';
            config.ProxyPort = parseInt(form.querySelector('.txtProxyPort').value, 10) || 0;
            config.ProxyUser = form.querySelector('.txtProxyUser').value || '';
            config.ProxyPass = form.querySelector('.txtProxyPass').value || '';

            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function () {

            return getTelegramEntry().then(function (entry) {
                if (!entry) {
                    return null;
                }
                entry.FriendlyName = friendlyName;
                return ApiClient.saveUserNotification(entry);
            });
        }).then(function () {
            loading.hide();
            if (window.Dashboard && Dashboard.processPluginConfigurationUpdateResult) {
                Dashboard.processPluginConfigurationUpdateResult();
            }
            toast('已保存');
        }, function () {
            loading.hide();
            toast('保存失败，请看 Emby 日志');
        });

        return false;
    }

    function onTest(e) {

        if (e) e.preventDefault();
        loading.show();

        var page = this;
        var options = {
            BotToken: page.querySelector('.txtTelegramBotToken').value,
            ChatID: page.querySelector('.txtTelegramChatID').value,
            DisablePoster: '' + page.querySelector('.chkTelegramDisablePoster').checked,
            DisableDescription: '' + page.querySelector('.chkTelegramDisableDescription').checked
        };

        ApiClient.sendTestUserNotification({ NotifierKey: notifierKey, Options: options }).then(function () {
            loading.hide();
            toast('测试消息已发送，去 Telegram 看看');
        }, function (err) {
            loading.hide();
            var message = err && (err.statusText || err.status || err.message);
            toast('测试失败：' + (message || '未知错误'));
        });

        return false;
    }

    function onFetchChatId(e) {

        if (e) e.preventDefault();

        var page = this;
        var token = page.querySelector('.txtTelegramBotToken').value;

        if (!token) {
            toast('先填 Bot Token');
            return false;
        }

        loading.show();

        fetch('https://api.telegram.org/bot' + token + '/getUpdates').then(function (response) {
            return response.json();
        }).then(function (data) {
            loading.hide();
            var chatId = null;
            var result = (data && data.result) || [];
            for (var i = 0; i < result.length; i++) {
                var item = result[i];
                if (item.message && item.message.chat) { chatId = item.message.chat.id; }
                else if (item.channel_post && item.channel_post.chat) { chatId = item.channel_post.chat.id; }
                else if (item.my_chat_member && item.my_chat_member.chat) { chatId = item.my_chat_member.chat.id; }
                if (chatId) { break; }
            }
            if (chatId) {
                page.querySelector('.txtTelegramChatID').value = chatId;
                toast('拿到 Chat ID：' + chatId + '（记得点保存）');
            } else {
                toast('没读到 Chat ID：先给 bot 发一条消息再点一次');
            }
        }, function () {
            loading.hide();
            toast('浏览器直连 api.telegram.org 失败（国内网络需代理）——手动填 Chat ID 即可');
        });

        return false;
    }

    /* Opens Emby's own notification entry editor (event subscriptions, users,
       libraries, devices, test button) for this user's Telegram entry. */
    function openEntryEditor() {

        loading.show();

        var userId = ApiClient.getCurrentUserId();
        var eventTypes = null;

        Promise.all([
            Emby.importModule('./settings/notificationeditor.js'),
            ApiClient.getJSON(ApiClient.getUrl('Notifications/Services/Configured', { UserId: userId })),
            ApiClient.getNotificationTypes({ userId: userId })
        ]).then(function (responses) {

            var NotificationEditor = responses[0];
            var entries = responses[1] || [];
            eventTypes = responses[2];

            var entry = null;
            for (var i = 0; i < entries.length; i++) {
                if (entries[i].NotifierKey === notifierKey) {
                    entry = entries[i];
                    break;
                }
            }

            var entryPromise = entry
                ? Promise.resolve(entry)
                : ApiClient.getJSON(ApiClient.getUrl('Notifications/Services/Defaults', { UserId: userId, NotifierKey: notifierKey }));

            return entryPromise.then(function (resolvedEntry) {
                return new Promise(function (resolve, reject) {
                    require([pluginManager.getConfigurationResourceUrl(resolvedEntry.SetupModuleUrl)], function (EntryFormEditor) {
                        resolve({ entry: resolvedEntry, EntryFormEditor: EntryFormEditor, NotificationEditor: NotificationEditor });
                    }, reject);
                });
            });
        }).then(function (context) {

            loading.hide();

            new context.NotificationEditor().show({
                entry: context.entry,
                apiClient: ApiClient,
                eventTypes: eventTypes,
                entryFormEditor: context.EntryFormEditor,
                userId: ApiClient.getCurrentUserId()
            });
        }, function (err) {
            loading.hide();
            toast('打不开通知编辑器：' + ((err && err.message) || '请看 Emby 日志'));
        });
    }

    function View(view, params) {

        BaseView.apply(this, arguments);

        var form = view.querySelector('form');
        if (form) {
            form.addEventListener('submit', onSubmit);
        }

        var bind = function (selector, handler) {
            var el = view.querySelector(selector);
            if (el) {
                el.addEventListener('click', handler.bind(view));
            }
        };

        bind('.btnOpenEntryEditor', function (e) { e.preventDefault(); openEntryEditor(); });
        bind('.btnTestTelegram', onTest);
        bind('.btnFetchChatID', onFetchChatId);
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function (options) {

        BaseView.prototype.onResume.apply(this, arguments);

        loading.show();

        var page = this.view;

        getConfig().then(function (response) {
            return loadPage(page, response);
        }).then(function () {
            loading.hide();
        }, function () {
            loading.hide();
        });
    };

    return View;
});
