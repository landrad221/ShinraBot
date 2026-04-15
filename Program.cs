// Load libraries
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Data.Sqlite;
using System;
using DotNetEnv;

namespace ShinraBot 
{

    class Program {
        static async Task Main() {

            // Load the .env file
            Env.Load(); 

            // Handler for Database
            var db = new DBHandler("Data Source=db/level.db");

            // --- BOT SETUP ---
            var discord = new DiscordClient(new DiscordConfiguration
            {
                Token = Environment.GetEnvironmentVariable("BOT_DISCORD_TOKEN"),
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
            });

            discord.MessageCreated += async (s, e) =>
            {
                if (e.Author.IsBot) return;

                ulong userId = e.Author.Id;
                int currentXp = db.UpdateUserXP(userId, 15); // Give 15 XP

                // Level calculation: Level = Square root of (XP / 100)
                // Level 1 = 100xp, Level 2 = 400xp, Level 3 = 900xp
                int oldLevel = (int)Math.Sqrt((currentXp - 15) / 100.0);
                int newLevel = (int)Math.Sqrt(currentXp / 100.0);

                if (newLevel > oldLevel)
                {
                    await e.Message.RespondAsync($"🌟 **LEVEL UP!** {e.Author.Mention} reached **Level {newLevel}**!");
                }
                    // Inside your message handler, if someone types !rank
                if (e.Message.Content.ToLower() == "!rank")
                {
                    var connection = db.Connection;
                    
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT XP FROM Users WHERE UserId = $id";
                    cmd.Parameters.Add("$id", SqliteType.Text).Value = e.Author.Id.ToString();

                    var result = cmd.ExecuteScalar();
                    int xp = result != null ? Convert.ToInt32(result) : 0;
                    int level = (int)Math.Sqrt(xp / 100.0);

                    await e.Message.RespondAsync($"{e.Author.Username}, you are **Level {level}** with **{xp} XP**!");
                }
            };

            await discord.ConnectAsync();
            await Task.Delay(-1);

        }
    }
}
