using DividendGuardian.Domain;
using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class StockRepository(Database database)
{
    public async Task<IReadOnlyList<Stock>> GetActiveWatchlistAsync(CancellationToken ct=default)
    {
        const string sql="select ticker,name,sector,subsector from stocks where is_active=true order by ticker;";
        var result=new List<Stock>();
        await using var connection=new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command=new NpgsqlCommand(sql,connection);
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
            result.Add(new Stock(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3)));
        return result;
    }
}