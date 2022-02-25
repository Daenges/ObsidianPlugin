/**
 * 
 * It is STRONGLY recommended to get familiar with both the API documentation and the basic plugin guide.
 * These can be found here:
 * https://obsidian-mc.net/articles/plugins.html
 * https://obsidian-mc.net/api/index.html
 * 
 * Please DO take note that Obsidian is still in active development,
 * meaning both the server and it's API are extremely prone to changes!
 * 
 */

using Obsidian.API;
using Obsidian.API.Events;
using Obsidian.API.Plugins;
using System;
using System.Threading.Tasks;

/// <summary>
/// Own imports
/// </summary>
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

namespace ObsidianPlugin
{
    [Plugin(name: "Mail", Version = "1.0",
            Authors = "Daenges", Description = "Save messages and send them to players when they are online again.",
            ProjectUrl = "https://github.com/ObsidianMC/Obsidian")]

    public class ObsidianPlugin : PluginBase
    {
        // One of server messages, called when an event occurs
        public void OnLoad(IServer server)
        {
            if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\plugins\\Mail"))
                Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\plugins\\Mail");
            if (!File.Exists(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json"))
                File.Create(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json");
        }

        /// <summary>
        /// Check if a given string is a valid Minecraft name.
        /// </summary>
        /// <param name="Name">String to check for validility.</param>
        /// <returns>whether the name is valid.</returns>
        private bool isValidMcName(string Name)
        {
            if (!string.IsNullOrEmpty(Name))
                return Regex.IsMatch(Name, @"^[a-zA-Z_\d]+");
            return false;
        }

        /// <summary>
        /// Deserializes mails from a file.
        /// </summary>
        /// <returns>A list of deserialized mails.</returns>
        private List<OwnClasses.Mail> getMailsFromFile()
        {
            if (File.Exists(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json"))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<OwnClasses.Mail>>(File.ReadAllText(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json"));
                }
                catch (JsonException) { Console.WriteLine("[Mail] Unable to read the Database. Creating a new one."); }
            }

            return new List<OwnClasses.Mail>();
        }

        /// <summary>
        /// Serializes a given mail list to JSON and saves the result in a file.
        /// </summary>
        /// <param name="mailList">provided Maillist</param>
        private void safeMailsToFile(List<OwnClasses.Mail> mailList)
        {
            File.WriteAllText(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json", JsonSerializer.Serialize(mailList));
        }

        [Command(commandName: "mail")]
        [CommandInfo(description: "mail <player> [content] - sends a message, that is saved until the adressed player joins.")]
        public async Task PluginCommandAsync(CommandContext ctx, [Remaining] string arguments)
        {
            List<OwnClasses.Mail> mailList = getMailsFromFile();

            string recipient = arguments.Split()[0];
            string content = arguments.Substring(recipient.Length + 1); 
            if (isValidMcName(recipient) && !string.IsNullOrEmpty(content))
            {
                mailList.Add(new OwnClasses.Mail
                {
                    Sender = ctx.Player == null ? "Server" : ctx.Player.Username.ToLower(),
                    Recipient = recipient.ToLower(),
                    Content = content,
                    TimeOfSending = DateTime.Now
                }
                );
            }

            safeMailsToFile(mailList);

            await ctx.Sender.SendMessageAsync(message: "Hello from plugin command implemented in Plugin class!");
        }

        public async Task OnPlayerJoin(PlayerJoinEventArgs playerJoinEvent)
        {
            string player = playerJoinEvent.Player.Username.ToLower();
            if (isValidMcName(player))
            {
                List<OwnClasses.Mail> mailList = getMailsFromFile();
                foreach (OwnClasses.Mail mail in mailList.Where(m => m.Recipient == player).ToArray())
                {
                    await playerJoinEvent.Player.SendMessageAsync(message: mail.Sender + " mailed: " + mail.Content);
                    mailList.Remove(mail);
                }
                safeMailsToFile(mailList);
            }
        }
    }
}

namespace OwnClasses
{
    public class Mail
    {
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public string Content { get; set; }
        public DateTime TimeOfSending { get; set; }
    }
}