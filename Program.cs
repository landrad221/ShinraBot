using DSharpPlus;
using DSharpPlus.SlashCommands;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ShinraBot
{
    class Program
    {
        static async Task Main()
        {
            if (File.Exists(".env"))
                Env.Load();

            var builder = Host.CreateApplicationBuilder();
            var provider = Environment.GetEnvironmentVariable("DATABASE_PROVIDER") ?? "sqlite";
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                ?? "Data Source=db/level.db";
            var database = new DBHandler(provider, connectionString);
            await database.InitializeAsync();

            var token = Environment.GetEnvironmentVariable("BOT_DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("BOT_DISCORD_TOKEN must be configured.");

            var discord = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents | DiscordIntents.GuildMembers,
                MessageCacheSize = 1000
            });

            builder.Services.AddSingleton(database);
            builder.Services.AddSingleton(discord);
            builder.Services.AddSingleton<BotServices>();
            using var host = builder.Build();
            var botServices = host.Services.GetRequiredService<BotServices>();

            var slashCommands = discord.UseSlashCommands(new SlashCommandsConfiguration
            {
                Services = host.Services
            });
            slashCommands.RegisterCommands<UtilityCommands>();
            slashCommands.RegisterCommands<ModerationCommands>();

            discord.MessageCreated += async (_, e) => await RunEventAsync(() => botServices.HandleMessageAsync(e));
            discord.MessageDeleted += async (_, e) => await RunEventAsync(() => botServices.LogDeletedMessageAsync(e));
            discord.GuildMemberAdded += async (_, e) => await RunEventAsync(() => botServices.LogMemberJoinedAsync(e));
            discord.GuildMemberRemoved += async (_, e) => await RunEventAsync(() => botServices.LogMemberLeftAsync(e));

            await discord.ConnectAsync();
            await Task.Delay(Timeout.Infinite);
        }

        private static async Task RunEventAsync(Func<Task> handler)
        {
            try
            {
                await handler();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
        }
    }
}
