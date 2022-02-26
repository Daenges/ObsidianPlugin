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

        public async Task OnPlayerJoin(PlayerJoinEventArgs playerJoinEvent)
        {
            string player = playerJoinEvent.Player.Username.ToLower();
            if (HelperFunctions.IsValidMcName(player))
            {
                List<Mail> mailList = HelperFunctions.GetMailsFromFile();
                foreach (Mail mail in mailList.Where(m => m.Recipient == player).ToArray())
                {
                    await playerJoinEvent.Player.SendMessageAsync(message: mail.Sender + " mailed: " + mail.Content);
                    mailList.Remove(mail);
                }

                // Clear mails older than 30 days.
                mailList = mailList.Where(mail => (DateTime.Now - mail.TimeOfSending).TotalDays < 31).ToList();

                HelperFunctions.SafeMailsToFile(mailList);
            }
        }
    }


    [CommandRoot]
    public class MyCommandRoot
    {
        [Command("mail")]
        [CommandInfo(description: "mail <player> [content] - Sends a message, that is saved until the adressed player joins.")]
        public async Task PluginCommandAsync(CommandContext ctx, [Remaining] string arguments)
        {
            string recipient = arguments.Split()[0].ToLower();
            string content = arguments.Substring(recipient.Length + 1);

            // Not dealing with this clown
            if (recipient == ctx.Player.Username.ToLower())
                return;

            // Serious requests
            if (ctx.Server.IsPlayerOnline(recipient))
            {
                await ctx.Server.GetPlayer(recipient)
                    .SendMessageAsync(message: (ctx.IsPlayer ? ctx.Player.Username.ToLower() : "Server") + " mailed: " + content);
                await ctx.Sender.SendMessageAsync(message: "The requested player is online! Forewarded message.");
            }
            else if (HelperFunctions.IsValidMcName(recipient) && !string.IsNullOrEmpty(content))
            {
                List<Mail> mailList = HelperFunctions.GetMailsFromFile();

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

                    HelperFunctions.SafeMailsToFile(mailList);
                    await ctx.Sender.SendMessageAsync(message: "Mail saved successfuly!");
                }
                else
                {
                    await ctx.Sender.SendMessageAsync(message: "You reached the personal maximum capacity of 5 mails. Wait until they get sent or delete one with '/delmail'.");
                }
            }
            else
            {
                await ctx.Sender.SendMessageAsync(message: "Mail could not be saved.");
            }
        }

        /// This is a command group. It can nest multiple commands, and other command groups "infinitely".
        [CommandGroup("delmail")]
        [CommandInfo("Delete Mails from Cache.")]
        public class MySubCommandRoot
        {
            [GroupCommand]
            public async Task DefaultCommandAsync(CommandContext ctx, [Remaining] string arguments)
            {
                string[] args = arguments.Split();
                string sender = ctx.Player.Username.ToLower();

                    int messageNum;
                    try
                    {
                        messageNum = Convert.ToInt32(args[0]);
                    } 
                    catch (Exception) 
                    {
                        await ctx.Player.SendMessageAsync(message: "Invalid number! Use '/delmail [Number]'");
                        return;
                    }

                    List<Mail> mailList = HelperFunctions.GetMailsFromFile();

                    // Find all mails of player and sort them like the display list.
                    List<Mail> mailsFromSender = HelperFunctions.GetMailsFromFile()
                        .Where(mail => mail.Sender == sender)
                        .OrderBy(mail => mail.Content.Substring(0, 10)).ToList();

                    // Delete the selected mail.
                    mailList.Remove(mailsFromSender[messageNum]);

                    HelperFunctions.SafeMailsToFile(mailList);
                
            }

            [Command("list")]
            [CommandInfo("List all your saved mails.")]
            public async Task SubCommandAsync(CommandContext ctx)
            {
                string sender = ctx.Player.Username.ToLower();

                List<string> mailContentList = HelperFunctions.GetMailsFromFile()
                        .Where(mail => mail.Sender == sender)
                        .Select(mail => mail.Content.Substring(0, 10)).ToList();
                mailContentList.Sort();

                string textListOfMails = string.Format("You have {0} mails saved:\n", mailContentList.Count);

                for (int mailIndex = 0; mailIndex < mailContentList.Count; mailIndex++)
                {
                    textListOfMails += string.Format("Mail[{0}]: {1}\n", mailIndex + 1, mailContentList[mailIndex]);
                }

                textListOfMails += "Use '/delmail [Number]' to delete one of your mails.";

                await ctx.Player.SendMessageAsync(message: textListOfMails);
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

    /// <summary>
    /// Functions needed for operation in several commands.
    /// </summary>
    static class HelperFunctions
    {
        // Helper functions:
        /// <summary>
        /// Check if a given string is a valid Minecraft name.
        /// </summary>
        /// <param name="Name">String to check for validility.</param>
        /// <returns>whether the name is valid.</returns>
        public static bool IsValidMcName(string Name)
        {
            if (!string.IsNullOrEmpty(Name))
                return Regex.IsMatch(Name, @"^[a-zA-Z_\d]+");
            return false;
        }

        /// <summary>
        /// Deserializes mails from a file.
        /// </summary>
        /// <returns>A list of deserialized mails.</returns>
        public static List<Mail> GetMailsFromFile()
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
        public static void SafeMailsToFile(List<Mail> mailList)
        {
            File.WriteAllText(Directory.GetCurrentDirectory() + "\\plugins\\Mail\\Mails.json", JsonSerializer.Serialize(mailList));
        }
    }
}