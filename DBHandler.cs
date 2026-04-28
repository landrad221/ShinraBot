using Microsoft.Data.Sqlite;
using System;

namespace ShinraBot 
{
    class DBHandler
    {
        private readonly SqliteConnection _connection ;

        public DBHandler(string connectionString) {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            var tableCmd = _connection.CreateCommand();
            tableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT PRIMARY KEY,
                XP INTEGER DEFAULT 0,
                TS TEXT
            )";
            tableCmd.ExecuteNonQuery();

            var walCmd = _connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;"; // Write-Ahead Logging
            walCmd.ExecuteNonQuery();
        }

        public int UpdateUserXP(ulong userId, int xpToAdd)
        {

            var command = _connection.CreateCommand();
            // We use CAST to ensure the ID is treated as a numeric value for the Primary Key
            command.CommandText = @"
                INSERT INTO Users (UserId, XP) 
                VALUES ($id, $xp)
                ON CONFLICT(UserId) 
                DO UPDATE SET XP = Users.XP + EXCLUDED.XP;
                SELECT XP FROM Users WHERE UserId = $id;";

            // Use .Add instead of .AddWithValue for better type safety
            command.Parameters.Add("$id", SqliteType.Text).Value = userId.ToString();
            command.Parameters.Add("$xp", SqliteType.Integer).Value = xpToAdd;

            // ExecuteScalar returns the first column of the first row (the new XP total)
            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        // Getter for db handler
        public SqliteConnection Connection => _connection;

        // Call this when the bot shuts down
        public void Close() => _connection.Close();
    }
}
