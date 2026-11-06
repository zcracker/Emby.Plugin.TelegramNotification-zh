define(['globalize', 'pluginManager', 'emby-input'], function (globalize, pluginManager) {
    'use strict';

    // Per-user notification entry editor.
    // Only the entry name is edited here: Bot Token / Chat ID / poster switches and
    // the proxy live in the plugin configuration page, so the same form is not
    // rendered twice in two different places.
    function EntryEditor() {
    }

    EntryEditor.setObjectValues = function (context, entry) {
        entry.FriendlyName = entry.FriendlyName || 'Telegram';
    };

    EntryEditor.setFormValues = function (context, entry) {
        // 名称、Token、Chat ID 都在插件设置页里编辑，这里没有输入框。
    };

    EntryEditor.loadTemplate = function (context) {
        return require(['text!' + pluginManager.getConfigurationResourceUrl('telegrameditortemplate')]).then(function (responses) {
            var template = responses[0];
            context.innerHTML = globalize.translateDocument(template);
        });
    };

    EntryEditor.destroy = function () {
    };

    return EntryEditor;
});
