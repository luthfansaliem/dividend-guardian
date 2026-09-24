using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class Database
{
    private readonly DatabaseOptions _options;

    public Database(DatabaseOptions options) => _options = options;

    public async Task<int> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select 1", connection);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }
}
