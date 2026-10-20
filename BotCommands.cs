using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace ShinraBot;

class UtilityCommands(DBHandler database) : ApplicationCommandModule
{
    [SlashCommand("ping", "Check the bot heartbeat latency")]
    public async Task PingAsync(InteractionContext context)
    {
        await RespondAsync(context, $"Pong! {context.Client.Ping} ms");
    }

    [SlashCommand("rank", "Display your level and XP")]
    public async Task RankAsync(InteractionContext context)
    {
        if (context.Guild is null)
        {
            await RespondAsync(context, "Esse comando só funciona em um servidor.");
            return;
        }

        var xp = await database.GetUserXpAsync(context.Guild.Id, context.User.Id);
        var level = (int)Math.Sqrt(xp / 100.0);
        var card = RankCardRenderer.Render(context.User.Username, level, xp);
        await context.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddFile("rank.png", new MemoryStream(card)));
    }

    private static Task RespondAsync(InteractionContext context, string message) =>
        context.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().WithContent(message));
}

class ModerationCommands(BotServices botServices) : ApplicationCommandModule
{
    [SlashCommand("ban", "Ban a member from this server")]
    [SlashCommandPermissions(Permissions.BanMembers)]
    public async Task BanAsync(
        InteractionContext context,
        [Option("member", "Member to ban")] DiscordUser user,
        [Option("reason", "Reason for the ban")] string reason = "No reason provided")
    {
        if (!await IsGuildCommandAsync(context))
            return;
        await context.Guild.BanMemberAsync(user.Id, 0, reason);
        await RespondAsync(context, $"{user.Username} foi banido. Motivo: {reason}");
    }

    [SlashCommand("kick", "Kick a member from this server")]
    [SlashCommandPermissions(Permissions.KickMembers)]
    public async Task KickAsync(
        InteractionContext context,
        [Option("member", "Member to kick")] DiscordUser user,
        [Option("reason", "Reason for the kick")] string reason = "No reason provided")
    {
        if (!await IsGuildCommandAsync(context))
            return;
        var member = await context.Guild.GetMemberAsync(user.Id);
        await member.RemoveAsync(reason);
        await RespondAsync(context, $"{user.Username} foi expulso. Motivo: {reason}");
    }

    [SlashCommand("timeout", "Temporarily timeout a member")]
    [SlashCommandPermissions(Permissions.ModerateMembers)]
    public Task TimeoutAsync(
        InteractionContext context,
        [Option("member", "Member to timeout")] DiscordUser user,
        [Option("minutes", "Timeout duration in minutes")] long minutes,
        [Option("reason", "Reason for the timeout")] string reason = "No reason provided") =>
        ApplyTimeoutAsync(context, user, minutes, reason);

    [SlashCommand("mute", "Temporarily mute a member using Discord timeout")]
    [SlashCommandPermissions(Permissions.ModerateMembers)]
    public Task MuteAsync(
        InteractionContext context,
        [Option("member", "Member to mute")] DiscordUser user,
        [Option("minutes", "Mute duration in minutes")] long minutes,
        [Option("reason", "Reason for the mute")] string reason = "No reason provided") =>
        ApplyTimeoutAsync(context, user, minutes, reason);

    private static async Task ApplyTimeoutAsync(InteractionContext context, DiscordUser user, long minutes, string reason)
    {
        if (!await IsGuildCommandAsync(context))
            return;
        if (minutes is < 1 or > 40320)
        {
            await RespondAsync(context, "A duração deve estar entre 1 e 40320 minutos.");
            return;
        }

        var member = await context.Guild.GetMemberAsync(user.Id);
        await member.TimeoutAsync(DateTimeOffset.UtcNow.AddMinutes(minutes), reason);
        await RespondAsync(context, $"{user.Username} recebeu timeout por {minutes} minutos. Motivo: {reason}");
    }

    [SlashCommand("reward-role", "Set a role reward for a level")]
    [SlashCommandPermissions(Permissions.ManageGuild)]
    public async Task RewardRoleAsync(
        InteractionContext context,
        [Option("level", "Level required for this role")] long level,
        [Option("role", "Role to grant")] DiscordRole role)
    {
        if (!await IsGuildCommandAsync(context))
            return;
        if (level is < 1 or > int.MaxValue)
        {
            await RespondAsync(context, "O nível deve ser maior que zero.");
            return;
        }

        await botServices.SetRoleRewardAsync(context.Guild.Id, (int)level, role.Id);
        await RespondAsync(context, $"{role.Mention} será concedido ao atingir o nível {level}.");
    }

    [SlashCommand("block-word", "Add a word to this server's auto-moderation filter")]
    [SlashCommandPermissions(Permissions.ManageGuild)]
    public async Task BlockWordAsync(
        InteractionContext context,
        [Option("word", "Word to block")] string word)
    {
        if (!await IsGuildCommandAsync(context))
            return;
        var normalizedWord = word.Trim();
        if (normalizedWord.Length is < 1 or > 100)
        {
            await RespondAsync(context, "A palavra deve ter entre 1 e 100 caracteres.");
            return;
        }

        await botServices.AddBlockedWordAsync(context.Guild.Id, normalizedWord);
        await RespondAsync(context, $"A palavra `{normalizedWord}` foi adicionada ao filtro.");
    }

    [SlashCommand("unblock-word", "Remove a word from this server's auto-moderation filter")]
    [SlashCommandPermissions(Permissions.ManageGuild)]
    public async Task UnblockWordAsync(
        InteractionContext context,
        [Option("word", "Word to unblock")] string word)
    {
        if (!await IsGuildCommandAsync(context))
            return;
        var normalizedWord = word.Trim();
        if (normalizedWord.Length is < 1 or > 100)
        {
            await RespondAsync(context, "A palavra deve ter entre 1 e 100 caracteres.");
            return;
        }

        await botServices.RemoveBlockedWordAsync(context.Guild.Id, normalizedWord);
        await RespondAsync(context, $"A palavra `{normalizedWord}` foi removida do filtro.");
    }

    private static async Task<bool> IsGuildCommandAsync(InteractionContext context)
    {
        if (context.Guild is not null)
            return true;
        await RespondAsync(context, "Esse comando só funciona em um servidor.");
        return false;
    }

    private static Task RespondAsync(InteractionContext context, string message) =>
        context.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().WithContent(message));
}