using System.Text;
using Dapper;
using UniverseLabs.Oms.DAL.Interfaces;
using UniverseLabs.Oms.DAL.Models;

namespace UniverseLabs.Oms.DAL.Repositories;

public class OrderRepository(UnitOfWork unitOfWork) : IOrderRepository
{
    public async Task<V1OrderDal[]> BulkInsert(V1OrderDal[] model, CancellationToken token)
    {
        var sql = @"
            insert into orders 
            (
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at
             )
            select 
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at
            from unnest(@Orders)
            returning 
                id,
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at;
        ";

        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1OrderDal>(new CommandDefinition(
            sql, new {Orders = model}, cancellationToken: token));
        
        return res.ToArray();
    }

    public async Task<V1OrderDal[]> Query(QueryOrdersDalModel model, CancellationToken token)
    {
        var sql = new StringBuilder(@"
            select 
                id,
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at
            from orders
        ");
        
        var param = new DynamicParameters();
        var conditions = new List<string>();

        if (model.Ids?.Length > 0)
        {
            param.Add("Ids", model.Ids);
            conditions.Add("id = ANY(@Ids)");
        }
        
        if (model.CustomerIds?.Length > 0)
        {
            param.Add("CustomerIds", model.CustomerIds);
            conditions.Add("customer_id = ANY(@CustomerIds)");
        }

        if (conditions.Count > 0)
        {
            sql.Append(" where " + string.Join(" and ", conditions));
        }

        if (model.Limit > 0)
        {
            sql.Append(" limit @Limit");
            param.Add("Limit", model.Limit);
        }

        if (model.Offset > 0)
        {
            sql.Append(" offset @Offset");
            param.Add("Offset", model.Offset);
        }
        
        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1OrderDal>(new CommandDefinition(
            sql.ToString(), param, cancellationToken: token));
        
        return res.ToArray();
    }
}