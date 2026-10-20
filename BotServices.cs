using System.Collections.Concurrent;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace ShinraBot;

class BotServices(DBHandler database, DiscordClient discord)
{
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), DateTimeOffset> _xpCooldowns = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), Queue<DateTimeOffset>> _recentMessages = new();
    private readonly ConcurrentDictionary<ulong, (DateTimeOffset Expires, string[] Words)> _wordCache = new();
    private readonly ulong? _logChannelId = ulong.TryParse(Environment.GetEnvironmentVariable("MOD_LOG_CHANNEL_ID"), out var channelId)
        ? channelId
        : null;

    public async Task HandleMessageAsync(MessageCreateEventArgs e)
    {
        if (e.Author.IsBot || e.Guild is null)
            return;

        var guildId = e.Guild.Id;
        var userId = e.Author.Id;
        var content = e.Message.Content;
        var tooManyMentions = e.Message.MentionedUsers.Count + e.Message.MentionedRoles.Count > 5
            || content.Contains("@everyone", StringComparison.OrdinalIgnoreCase)
            || content.Contains("@here", StringComparison.OrdinalIgnoreCase);
        var isSpam = TrackMessage(guildId, userId);
        var blockedWord = (await GetBlockedWordsAsync(guildId))
            .FirstOrDefault(word => content.Contains(word, StringComparison.OrdinalIgnoreCase));

        if (tooManyMentions || isSpam || blockedWord is not null)
        {
            await e.Message.DeleteAsync();
            var reason = tooManyMentions ? "mass mention" : isSpam ? "spam" : $"blocked word: {blockedWord}";
            await LogAsync(guildId, $"Auto-Mod removed a message from {e.Author.Mention} ({reason}).");
            await e.Channel.SendMessageAsync($"{e.Author.Mention}, sua mensagem foi removida pelos filtros de moderação.");
            return;
        }

        var key = (guildId, userId);
        var now = DateTimeOffset.UtcNow;
        var lastAward = _xpCooldowns.GetOrAdd(key, DateTimeOffset.MinValue);
        if (now - lastAward < TimeSpan.FromSeconds(60) || !_xpCooldowns.TryUpdate(key, now, lastAward))
            return;

        _xpCooldowns[key] = now;
        var xpAward = Random.Shared.Next(15, 26);
        var newXp = await database.AddUserXpAsync(guildId, userId, xpAward);
        var oldXp = newXp - xpAward;
        var oldLevel = (int)Math.Sqrt(oldXp / 100.0);
        var newLevel = (int)Math.Sqrt(newXp / 100.0);
        if (newLevel <= oldLevel)
            return;

        await e.Message.RespondAsync($"🌟 **LEVEL UP!** {e.Author.Mention} reached **Level {newLevel}**!");
        var member = await e.Guild.GetMemberAsync(userId);
        foreach (var reward in await database.GetRoleRewardsAsync(guildId, newLevel))
        {
            var role = e.Guild.GetRole(reward.RoleId);
            if (!member.Roles.Any(existing => existing.Id == role.Id))
                await member.GrantRoleAsync(role, $"Reached level {reward.Level}");
        }
    }

    public async Task LogDeletedMessageAsync(MessageDeleteEventArgs e)
    {
        if (e.Guild is null || e.Message is null || e.Message.Author.IsBot)
            return;
        await LogAsync(e.Guild.Id, $"Deleted message by {e.Message.Author.Username} in {e.Channel.Mention}: {Truncate(e.Message.Content, 900)}");
    }

    public Task LogMemberJoinedAsync(GuildMemberAddEventArgs e) =>
        LogAsync(e.Guild.Id, $"Member joined: {e.Member.Username} ({e.Member.Id}).");

    public Task LogMemberLeftAsync(GuildMemberRemoveEventArgs e) =>
        LogAsync(e.Guild.Id, $"Member left: {e.Member.Username} ({e.Member.Id}).");

    public async Task SetRoleRewardAsync(ulong guildId, int level, ulong roleId) =>
        await database.SetRoleRewardAsync(guildId, level, roleId);

    public async Task AddBlockedWordAsync(ulong guildId, string word)
    {
        await database.AddBlacklistedWordAsync(guildId, word);
        _wordCache.TryRemove(guildId, out _);
    }

    public async Task RemoveBlockedWordAsync(ulong guildId, string word)
    {
        await database.RemoveBlacklistedWordAsync(guildId, word);
        _wordCache.TryRemove(guildId, out _);
    }

    private bool TrackMessage(ulong guildId, ulong userId)
    {
        var queue = _recentMessages.GetOrAdd((guildId, userId), _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            var now = DateTimeOffset.UtcNow;
            while (queue.Count > 0 && now - queue.Peek() > TimeSpan.FromSeconds(8))
                queue.Dequeue();
            queue.Enqueue(now);
            return queue.Count >= 6;
        }
    }

    private async Task<string[]> GetBlockedWordsAsync(ulong guildId)
    {
        var now = DateTimeOffset.UtcNow;
        if (_wordCache.TryGetValue(guildId, out var cached) && cached.Expires > now)
            return cached.Words;

        var words = (await database.GetBlacklistedWordsAsync(guildId)).ToArray();
        _wordCache[guildId] = (now.AddSeconds(30), words);
        return words;
    }

    private async Task LogAsync(ulong guildId, string message)
    {
        if (_logChannelId is null)
            return;
        var channel = await discord.GetChannelAsync(_logChannelId.Value);
        if (channel.GuildId == guildId)
            await channel.SendMessageAsync(message);
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "...";
}