using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using UniverseLabs.Oms.DAL.Models;

namespace UniverseLabs.Oms.DAL;

public class UnitOfWork(IOptions<DbSettings> dbSettings): IDisposable
{
    private NpgsqlConnection _connection;
    
    public async Task<NpgsqlConnection> GetConnection(CancellationToken token)
    {
        if (_connection is not null)
        {
            return _connection;
        }
        
        var dataSource = new NpgsqlDataSourceBuilder(dbSettings.Value.ConnectionString);
        dataSource.MapComposite<V1OrderDal>("v1_order");
        dataSource.MapComposite<V1OrderItemDal>("v1_order_item");
        
        _connection = dataSource.Build().CreateConnection();

        _connection.StateChange += (sender, args) =>
        {
            if (args.CurrentState == ConnectionState.Closed)
                _connection = null;
        };
        
        await _connection.OpenAsync(token);

        return _connection;
    }

    public async ValueTask<NpgsqlTransaction> BeginTransactionAsync(CancellationToken token)
    {
        _connection ??= await GetConnection(token);
        return await _connection.BeginTransactionAsync(token);
    }

    public void Dispose()
    {
        DisposeConnection();
        GC.SuppressFinalize(this);
    }
    
    ~UnitOfWork()
    {
        DisposeConnection();
    }
    
    private void DisposeConnection()
    {
        _connection?.Dispose();
        _connection = null;
    }
}