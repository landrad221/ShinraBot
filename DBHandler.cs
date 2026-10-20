using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace ShinraBot;

class DBHandler
{
    private readonly string _connectionString;
    private readonly bool _usePostgres;

    public DBHandler(string provider, string connectionString)
    {
        var usePostgres = string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "postgresql", StringComparison.OrdinalIgnoreCase);
        if (!usePostgres && !string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("DATABASE_PROVIDER must be 'sqlite' or 'postgres'.", nameof(provider));

        _usePostgres = usePostgres;
        _connectionString = connectionString;
        if (!_usePostgres)
        {
            var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (dataSource != ":memory:")
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dataSource))!);
        }
    }

    public async Task InitializeAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        if (!_usePostgres)
        {
            command.CommandText = "PRAGMA journal_mode=WAL";
            await command.ExecuteNonQueryAsync();
        }

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        command.CommandText = _usePostgres
            ? "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'users'"
            : "PRAGMA table_info(Users)";
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(_usePostgres ? 0 : 1));
        }

        var migratedLegacyUsers = columns.Count > 0 && !columns.Contains("GuildId");
        if (migratedLegacyUsers)
        {
            command.CommandText = "ALTER TABLE Users RENAME TO LegacyUsers";
            await command.ExecuteNonQueryAsync();
        }

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                GuildId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                XP INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (GuildId, UserId)
            );
            CREATE TABLE IF NOT EXISTS RoleRewards (
                GuildId TEXT NOT NULL,
                Level INTEGER NOT NULL,
                RoleId TEXT NOT NULL,
                PRIMARY KEY (GuildId, Level)
            );
            CREATE TABLE IF NOT EXISTS BlacklistedWords (
                GuildId TEXT NOT NULL,
                Word TEXT NOT NULL,
                PRIMARY KEY (GuildId, Word)
            );
            """;
        await command.ExecuteNonQueryAsync();

        if (migratedLegacyUsers)
        {
            command.CommandText = "INSERT INTO Users (GuildId, UserId, XP) SELECT '0', UserId, XP FROM LegacyUsers";
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> GetUserXpAsync(ulong guildId, ulong userId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT XP FROM Users
            WHERE UserId = @userId AND GuildId IN (@guildId, '0')
            ORDER BY CASE WHEN GuildId = @guildId THEN 0 ELSE 1 END
            LIMIT 1
            """;
        AddParameter(command, "@guildId", guildId.ToString());
        AddParameter(command, "@userId", userId.ToString());
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    public async Task<int> AddUserXpAsync(ulong guildId, ulong userId, int amount)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (GuildId, UserId, XP)
            VALUES (
                @guildId,
                @userId,
                COALESCE((SELECT XP FROM Users WHERE GuildId = '0' AND UserId = @userId), 0) + @amount
            )
            ON CONFLICT (GuildId, UserId)
            DO UPDATE SET XP = Users.XP + EXCLUDED.XP
            RETURNING XP;
            """;
        AddParameter(command, "@guildId", guildId.ToString());
        AddParameter(command, "@userId", userId.ToString());
        AddParameter(command, "@amount", amount);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task SetRoleRewardAsync(ulong guildId, int level, ulong roleId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RoleRewards (GuildId, Level, RoleId)
            VALUES (@guildId, @level, @roleId)
            ON CONFLICT (GuildId, Level)
            DO UPDATE SET RoleId = EXCLUDED.RoleId;
            """;
        AddParameter(command, "@guildId", guildId.ToString());
        AddParameter(command, "@level", level);
        AddParameter(command, "@roleId", roleId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<(int Level, ulong RoleId)>> GetRoleRewardsAsync(ulong guildId, int level)
    {
        var rewards = new List<(int Level, ulong RoleId)>();
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Level, RoleId FROM RoleRewards WHERE GuildId = @guildId AND Level <= @level ORDER BY Level";
        AddParameter(command, "@guildId", guildId.ToString());
        AddParameter(command, "@level", level);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rewards.Add((reader.GetInt32(0), ulong.Parse(reader.GetString(1))));
        return rewards;
    }

    public async Task AddBlacklistedWordAsync(ulong guildId, string word)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO BlacklistedWords (GuildId, Word) VALUES (@guildId, @word) ON CONFLICT (GuildId, Word) DO NOTHING";
        AddParameter(command, "@guildId", guildId.ToString());
        AddParameter(command, "@word", word);
        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveBlacklistedWordAsync(ulong guildId, string word)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BlacklistedWords WHERE GuildId = @guildId AND Word = @word";
        AddParameter(command, "@guildId", guildId.ToString());
        AddParameter(command, "@word", word);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<string>> GetBlacklistedWordsAsync(ulong guildId)
    {
        var words = new List<string>();
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Word FROM BlacklistedWords WHERE GuildId = @guildId";
        AddParameter(command, "@guildId", guildId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            words.Add(reader.GetString(0));
        return words;
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        DbConnection connection = _usePostgres
            ? new NpgsqlConnection(_connectionString)
            : new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}