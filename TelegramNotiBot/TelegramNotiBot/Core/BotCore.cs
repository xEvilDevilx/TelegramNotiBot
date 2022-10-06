using System;
using System.Collections.Generic;
using System.IO;
using File = System.IO.File;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramNotiBot.Constants;
using TelegramNotiBot.Models;

namespace TelegramNotiBot.Core
{
    /// <summary>
    /// Implements Bot Engine functionality
    /// </summary>
    public class BotCore
    {
        private static ILog _log;
        private static ITelegramBotClient _client;
        private static Dictionary<string, long> _chatIDs;
        private static bool _isLive;
        private static long _lastMessageID;
        private static bool _isEnableStopCommand;

        /// <summary>
        /// Use for Start Bot working
        /// </summary>
        /// <returns></returns>
        public static async Task Start()
        {
            Init();

            _log.Info("Begin");

            try
            {
                await _client.SetWebhookAsync("");
                int offset = 0;
                _log.Info("Bot Webhook async is starting");

                while (_isLive)
                {
                    var updates = await _client.GetUpdatesAsync(offset);
                    GetGroups(updates);
                    offset = await HandleMessages(offset, updates);
                }

                _log.Info("End");
            }
            catch(Exception ex)
            {
                _log.Error("Error!", ex);
            }
        }

        /// <summary>
        /// Use for Init Bot Core
        /// </summary>
        private static void Init()
        {
            _log = LogManager.GetLogger(typeof(BotCore));
            _log.Info("Begin");

            _isEnableStopCommand = false;
            _lastMessageID = LoadLastMessageID();
            _isLive = true;
            _chatIDs = LoadGroupIDs();

            _client = new TelegramBotClient(AppSettings.Key);

            _log.Info("End");
        }

        private static void GetGroups(Update[] updates)
        {
            foreach (var update in updates)
            {
                if (update.Message == null || update.Message.Chat == null)
                    continue;

                if (update.Message.Chat.Type == ChatType.Group || update.Message.Chat.Type == ChatType.Supergroup)
                {
                    if (_chatIDs.ContainsKey(update.Message.Chat.Title))
                        continue;

                    _chatIDs.Add(update.Message.Chat.Title, update.Message.Chat.Id);
                    var group = new Group()
                    {
                        GroupName = update.Message.Chat.Title,
                        GroupID = update.Message.Chat.Id
                    };
                    SaveGroupIDs(group);
                }
            }
        }

        private static async Task<int> HandleMessages(int offset, Update[] updates)
        {
            foreach (var update in updates)
            {
                offset = update.Id + 1;
                if (update.Type == UpdateType.Message)
                {
                    if (string.IsNullOrEmpty(update.Message.Text))
                        continue;

                    if (_lastMessageID < update.Message.MessageId)
                    {
                        SaveLastMessageID(update.Message.MessageId);
                        _lastMessageID = update.Message.MessageId;
                    }
                    else continue;

                    if (_isEnableStopCommand && update.Message.Text == Commands.Stop)
                    {
                        _isLive = false;
                        break;
                    }

                    if (!ValidateMessage(update.Message.Text))
                    {
                        _log.InfoFormat("Unknown message: '{0}'", update.Message.Text);
                        continue;
                    }

                    var result = MessageParse(update.Message.Text);

                    if (!_chatIDs.ContainsKey(result.ChatName))
                    {
                        _log.InfoFormat("Unknown chat group name: '{0}'", result.ChatName);
                        await _client.SendTextMessageAsync(new ChatId(update.Message.Chat.Id),
                            string.Format("Bot is not a member of the group '{0}'", result.ChatName));
                    }
                    else
                    {
                        await _client.SendTextMessageAsync(new ChatId(_chatIDs[result.ChatName]), result.Message);
                        _log.InfoFormat("Message '{0}' to group '{1}' was sent", result.Message, result.ChatName);
                    }
                }
            }

            return offset;
        }

        /// <summary>
        /// Use for Send Message from bot
        /// </summary>
        /// <param name="message">Message text</param>
        /// <param name="chatName">Chat name for send message</param>
        /// <returns></returns>
        public static async Task<string> SendMessage(string message, string chatName)
        {
            try
            {
                if (_client == null)
                    Init();

                _log.Info("Begin");

                if (_chatIDs.ContainsKey(chatName))
                {
                    await _client.SendTextMessageAsync(new ChatId(_chatIDs[chatName]), message);
                    _log.InfoFormat("Message '{0}' to group '{1}' was sent", message, chatName);
                    return "Message was sent";
                }
                else return "Unknown chat name";
            }
            catch (Exception ex)
            {
                var msg = string.Format("Error occured while bot is tryed to send message, details: {0}, stack trace: {1}",
                    ex.Message, ex.StackTrace);
                _log.Error(msg);
                return msg;
            }
            finally
            {
                _log.Info("End");
            }
        }

        /// <summary>
        /// Use for Validate Message for a 'Message' type
        /// </summary>
        /// <param name="message">JSON message string</param>
        /// <returns></returns>
        private static bool ValidateMessage(string message)
        {            
            if (message.Contains("message") && message.Contains("ChatName"))
                return true;
            else return false;
        }

        /// <summary>
        /// Use for Parse json string to business object
        /// </summary>
        /// <param name="message">JSON message string</param>
        /// <returns></returns>
        private static JsonMessage MessageParse(string message)
        {
            _log.Info("Begin/End");
            var obj = JsonConvert.DeserializeObject<JsonMessage>(message);
            return obj;
        }

        /// <summary>
        /// Use for Load Group IDs
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, long> LoadGroupIDs()
        {
            _log.Info("Begin");

            try
            {
                List<Group> groups;
                var groupsDic = new Dictionary<string, long>();
                var rootDir = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
                var appSettingsPath = Path.Combine(rootDir, AppSettings.NotiBotFolderName);
                var filePath = Path.Combine(appSettingsPath, AppSettings.GroupIDsFileName);
                if (!File.Exists(filePath))
                {
                    _log.Info("End. Groups json file doesn't exists.");
                    return groupsDic;
                }

                using (var r = new StreamReader(filePath))
                {
                    string json = r.ReadToEnd();
                    groups = JsonConvert.DeserializeObject<List<Group>>(json);
                }

                if (groups != null)
                {
                    foreach (var group in groups)
                        groupsDic.Add(group.GroupName, group.GroupID);
                }

                _log.Info("End");

                return groupsDic;
            }
            catch (Exception ex)
            {
                _log.Error("Error!", ex);
                throw;
            }
        }

        /// <summary>
        /// Use for Save GroupIDs
        /// </summary>
        /// <param name="group">Group for add to groups list</param>
        private static void SaveGroupIDs(Group group)
        {
            _log.Info("Begin");

            try
            {
                var groups = LoadGroupIDs();
                var groupList = new List<Group>();
                foreach(var groupItem in groups)
                {
                    var groupObj = new Group()
                    {
                        GroupName = groupItem.Key,
                        GroupID = groupItem.Value
                    };
                    groupList.Add(groupObj);
                }
                groupList.Add(group);

                var rootDir = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
                var appSettingsPath = Path.Combine(rootDir, AppSettings.NotiBotFolderName);

                if (!Directory.Exists(appSettingsPath))
                    Directory.CreateDirectory(appSettingsPath);

                var filePath = Path.Combine(appSettingsPath, AppSettings.GroupIDsFileName);

                if (!File.Exists(filePath))
                    using (CreateFileFullAccess(filePath)) { };

                string json = JsonConvert.SerializeObject(groupList);
                File.WriteAllText(filePath, json);

                _log.Info("End");
            }
            catch (Exception ex)
            {
                _log.Error("Error!", ex);
                throw;
            }
        }

        /// <summary>
        /// Use for Load Last Message ID
        /// </summary>
        /// <returns></returns>
        private static long LoadLastMessageID()
        {
            _log.Info("Begin");

            try
            {
                MessageID messageID;
                var rootDir = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
                var appSettingsPath = Path.Combine(rootDir, AppSettings.NotiBotFolderName);
                var filePath = Path.Combine(appSettingsPath, AppSettings.LastMessageIDFileName);
                if (!File.Exists(filePath))
                {
                    _log.Info("End. Last Message json file doesn't exists.");
                    return 0;
                }

                using (var r = new StreamReader(filePath))
                {
                    string json = r.ReadToEnd();
                    messageID = JsonConvert.DeserializeObject<MessageID>(json);
                }

                _log.Info("End");
                return messageID == null ? 0 : messageID.LastMessageID;
            }
            catch (Exception ex)
            {
                _log.Error("Error!", ex);
                throw;
            }
        }

        /// <summary>
        /// Use for Save Last Message ID
        /// </summary>
        /// <param name="lastMessageID">Last Message ID</param>
        private static void SaveLastMessageID(long lastMessageID)
        {
            _log.Info("Begin");

            try
            {
                var messageID = new MessageID()
                {
                    LastMessageID = lastMessageID
                };

                var rootDir = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
                var appSettingsPath = Path.Combine(rootDir, AppSettings.NotiBotFolderName);

                if (!Directory.Exists(appSettingsPath))
                    Directory.CreateDirectory(appSettingsPath);

                var filePath = Path.Combine(appSettingsPath, AppSettings.LastMessageIDFileName);

                if (!File.Exists(filePath))
                    using (CreateFileFullAccess(filePath)) { } ;

                string json = JsonConvert.SerializeObject(messageID);
                File.WriteAllText(filePath, json);

                _log.Info("End");
            }
            catch (Exception ex)
            {
                _log.Error("Error!", ex);
                throw;
            }
        }

        /// <summary>
        /// Use for Create File Full Access path
        /// </summary>
        /// <param name="path">File path</param>
        /// <returns></returns>
        private static FileStream CreateFileFullAccess(string path)
        {
            _log.Info("Begin");

            try
            {
                var securityRules = new FileSecurity();
                var everyOne = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                securityRules.AddAccessRule(new FileSystemAccessRule(everyOne, FileSystemRights.FullControl, AccessControlType.Allow));

                var fs = File.Create(path, 1024, FileOptions.Asynchronous, securityRules);

                _log.Info("End");
                return fs;
            }
            catch(Exception ex)
            {
                _log.Error("Error!", ex);
                throw;
            }
        }
    }
}