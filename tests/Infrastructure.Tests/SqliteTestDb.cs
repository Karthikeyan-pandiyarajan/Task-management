using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

// Real SQLite (in-memory) rather than the EF InMemory provider, so unique indexes, FKs and
// SQL translation behave exactly as in production. The database lives as long as the open connection.
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDb()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
