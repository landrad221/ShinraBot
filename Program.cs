using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Data.Sqlite;
using System;
using DotNetEnv; // 1. Add this

// 2. Load the .env file
Env.Load();

// --- DATABASE SETUP ---
string connectionString = "Data Source=db/level.db";

// Create the table if it doesn't exist at startup
using (var connection = new SqliteConnection(connectionString))
{
    connection.Open();
    var tableCmd = connection.CreateCommand();
    /*tableCmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Users (
            UserId CAST(TEXT AS UNSIGNED INT) PRIMARY KEY, 
            XP INTEGER DEFAULT 0
        )"; */
    tableCmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS Users (
        UserId INTEGER PRIMARY KEY, 
        XP INTEGER DEFAULT 0
    )";
    tableCmd.ExecuteNonQuery();
}

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

    long userId = (long)e.Author.Id;
    int currentXp = UpdateUserXP(userId, 15); // Give 15 XP

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
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT XP FROM Users WHERE UserId = $id";
        cmd.Parameters.Add("$id", SqliteType.Integer).Value = (long)e.Author.Id;

        var result = cmd.ExecuteScalar();
        int xp = result != null ? Convert.ToInt32(result) : 0;
        int level = (int)Math.Sqrt(xp / 100.0);

        await e.Message.RespondAsync($"{e.Author.Username}, you are **Level {level}** with **{xp} XP**!");
    }

};

await discord.ConnectAsync();
await Task.Delay(-1);

// --- HELPER FUNCTION ---
int UpdateUserXP(long userId, int xpToAdd)
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    var command = connection.CreateCommand();
    // We use CAST to ensure the ID is treated as a numeric value for the Primary Key
    command.CommandText = @"
        INSERT INTO Users (UserId, XP) 
        VALUES ($id, $xp)
        ON CONFLICT(UserId) 
        DO UPDATE SET XP = Users.XP + EXCLUDED.XP;
        
        SELECT XP FROM Users WHERE UserId = $id;";

    // Use .Add instead of .AddWithValue for better type safety
    command.Parameters.Add("$id", SqliteType.Integer).Value = (long)userId;
    command.Parameters.Add("$xp", SqliteType.Integer).Value = xpToAdd;

    // ExecuteScalar returns the first column of the first row (the new XP total)
    var result = command.ExecuteScalar();
    return Convert.ToInt32(result);
}
