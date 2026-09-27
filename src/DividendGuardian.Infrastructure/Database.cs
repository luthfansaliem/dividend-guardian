using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class Database
{
    private readonly DatabaseOptions _options;

    public Database(DatabaseOptions options) => _options = options;

    public string ConnectionString => NormalizeConnectionString(_options.ConnectionString);

    public async Task<int> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select 1", connection);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    private static string NormalizeConnectionString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (!value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0)
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        if (userInfo.Length > 1)
            builder.Password = Uri.UnescapeDataString(userInfo[1]);

        return builder.ConnectionString;
    }
}
