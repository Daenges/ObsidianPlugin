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
            ProjectUrl = "https://github.com/Daenges/ObsidianPlugin")]

    public class ObsidianPlugin : PluginBase
    {
        /// <summary>
        /// Prepare folder and file for operation.
        /// </summary>
        /// <param name="server"></param>
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
        private List<Mail> getMailsFromFile()
        {
            if (File.Exists(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json"))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<Mail>>(File.ReadAllText(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json"));
                }
                catch (JsonException) { Console.WriteLine("[Mail] Unable to read the Database. Creating a new one."); }
            }

            return new List<Mail>();
        }

        /// <summary>
        /// Serializes a given mail list to JSON and saves the result in a file.
        /// </summary>
        /// <param name="mailList">provided Maillist</param>
        private void safeMailsToFile(List<Mail> mailList)
        {
            File.WriteAllText(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json", JsonSerializer.Serialize(mailList));
        }

        [Command(commandName: "mail")]
        [CommandInfo(description: "mail <player> [content] - Sends a message, that is saved until the adressed player joins.")]
        public async Task PluginCommandAsync(CommandContext ctx, [Remaining] string arguments)
        {
            string recipient = arguments.Split()[0].ToLower();
            string content = arguments.Substring(recipient.Length + 1);

            // Not dealing with this
            if (recipient == ctx.Player.Username.ToLower())
                return;

            // Serious requests
            if (ctx.Server.IsPlayerOnline(recipient)) 
            {
                await ctx.Server.GetPlayer(recipient)
                    .SendMessageAsync(message: (ctx.IsPlayer ? ctx.Player.Username.ToLower() : "Server") + " mailed: " + content);
                await ctx.Sender.SendMessageAsync(message: "The requested player is online! Forewarded message.");
            }
            else if (isValidMcName(recipient) && !string.IsNullOrEmpty(content))
            {
                List<Mail> mailList = getMailsFromFile();

                if (mailList.Where(mail => mail.Sender == ctx.Player.Username.ToLower()).Count() < 5)
                {
                    mailList.Add(new Mail
                    {
                        Sender = ctx.IsPlayer ? ctx.Player.Username.ToLower() : "Server",
                        Recipient = recipient,
                        Content = content,
                        TimeOfSending = DateTime.Now
                    }
                    );

                    safeMailsToFile(mailList);
                    await ctx.Sender.SendMessageAsync(message: "Mail saved successfuly!");
                }
                else 
                {
                    await ctx.Sender.SendMessageAsync(message: "You reached the personal maximum capacity of 5 mails.");
                }
            }
            else 
            {
                await ctx.Sender.SendMessageAsync(message: "Mail could not be saved.");
            }
        }

        public async Task OnPlayerJoin(PlayerJoinEventArgs playerJoinEvent)
        {
            string player = playerJoinEvent.Player.Username.ToLower();
            if (isValidMcName(player))
            {
                List<Mail> mailList = getMailsFromFile();
                foreach (Mail mail in mailList.Where(m => m.Recipient == player).ToArray())
                {
                    await playerJoinEvent.Player.SendMessageAsync(message: mail.Sender + " mailed: " + mail.Content);
                    mailList.Remove(mail);
                }

                // Clear mails older than 30 days.
                mailList = mailList.Where(mail => (DateTime.Now - mail.TimeOfSending).TotalDays < 31).ToList();

                safeMailsToFile(mailList);
            }
        }
    }

    /// <summary>
    /// Class to save messages in.
    /// </summary>
    public class Mail
    {
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public string Content { get; set; }
        public DateTime TimeOfSending { get; set; }
    }
}