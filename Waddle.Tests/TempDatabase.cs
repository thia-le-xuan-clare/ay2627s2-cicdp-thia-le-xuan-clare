using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Waddle.Infrastructure.Data;

namespace Waddle.Tests;

/// <summary>A throwaway SQLite file with the real schema. Each test class instance gets its own.</summary>
public sealed class TempDatabase : IDisposable
{
    public string FilePath { get; }
    public DatabaseInitializer Db { get; }

    public TempDatabase()
    {
        FilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"waddle-test-{Guid.NewGuid():N}.db");
        Db = new DatabaseInitializer(FilePath);
        Db.Initialize();
    }

    /// <summary>Runs a query and returns the first column of the first row.</summary>
    public T Scalar<T>(string sql)
    {
        using var connection = Db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    /// <summary>Runs an INSERT/UPDATE/DELETE straight against the database.</summary>
    public int Execute(string sql)
    {
        using var connection = Db.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();   // release the file so it can be deleted
        foreach (var f in new[] { FilePath, FilePath + "-wal", FilePath + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }
}
