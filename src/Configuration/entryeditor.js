define(['globalize', 'pluginManager', 'emby-input'], function (globalize, pluginManager) {
    'use strict';

    function EntryEditor() {
    }

    EntryEditor.setObjectValues = function (context, entry) {
        entry.FriendlyName = context.querySelector('.txtFriendlyName').value;
        entry.Options.BotToken = context.querySelector('.txtTeleGramBotKey').value;
        entry.Options.ChatID = context.querySelector('.txtTeleGramChatID').value;
        entry.Options.DisablePoster = '' + context.querySelector('.chkDisablePoster').checked;
        entry.Options.DisableDescription = '' + context.querySelector('.chkDisableDescription').checked;
    };

    EntryEditor.setFormValues = function (context, entry) {
        context.querySelector('.txtFriendlyName').value = entry.FriendlyName || '';
        context.querySelector('.txtTeleGramBotKey').value = entry.Options.BotToken || '';
        context.querySelector('.txtTeleGramChatID').value = entry.Options.ChatID || '';
        context.querySelector('.chkDisablePoster').checked = entry.Options.DisablePoster == "true" || false;
        context.querySelector('.chkDisableDescription').checked = entry.Options.DisableDescription == "true" || false;
    };

    EntryEditor.fetchChatID = async function (context) {
        const botToken = context.querySelector('.txtTeleGramBotKey').value;
        
        if (!botToken) {
            alert('Please enter your bot token first!');
            return;
        }

        try {
            const response = await fetch(`https://api.telegram.org/bot${botToken}/getUpdates`);
            
            if (!response.ok) {
                throw new Error('Error getting ChatID. Have you messaged your bot?');
            }
            
            const data = await response.json();
            
            if (data.ok && data.result && data.result.length > 0) {
                let chatId = null;
                
                // Try to read message.chat.id
                if (data.result[0].message && data.result[0].message.chat) {
                    chatId = data.result[0].message.chat.id;
                }
                // Last resort my_chat_member.chat.id
                else if (data.result[0].my_chat_member && data.result[0].my_chat_member.chat) {
                    chatId = data.result[0].my_chat_member.chat.id;
                }
                
                if (chatId) {
                    context.querySelector('.txtTeleGramChatID').value = chatId;
                    alert('Successfully retrieved Chat ID: ' + chatId);
                } else {
                    alert('No Chat-ID found. Send a message to your bot or add it to a group!');
                }
            } else {
                alert('No Chat-ID found. Send a message to your bot or add it to a group!');
            }
            
        } catch (error) {
            alert('Error retrieving the Chat-ID: ' + error.message);
        }
    };

    EntryEditor.loadTemplate = function (context) {
        return require(['text!' + pluginManager.getConfigurationResourceUrl('telegrameditortemplate')]).then(function (responses) {
            var template = responses[0];
            context.innerHTML = globalize.translateDocument(template);
            
            // Event Handler für den Chat-ID-Abruf-Button
            const fetchButton = context.querySelector('.btnFetchChatID');
            if (fetchButton) {
                fetchButton.addEventListener('click', function () {
                    EntryEditor.fetchChatID(context);
                });
            }
        });
    };

    EntryEditor.destroy = function () {
    };

    return EntryEditor;
});