using System.Text;
using Dapper;
using UniverseLabs.Oms.DAL.Interfaces;
using UniverseLabs.Oms.DAL.Models;

namespace UniverseLabs.Oms.DAL.Repositories;

public class OrderItemRepository(UnitOfWork unitOfWork) : IOrderItemRepository
{
    public async Task<V1OrderItemDal[]> BulkInsert(V1OrderItemDal[] models, CancellationToken token)
    {
        var sql = @"
            insert into order_items 
            (
                order_id,
                product_id,
                quantity,
                product_title,
                product_url,
                price_cents,
                price_currency,
                created_at,
                updated_at
             )
            select 
                order_id,
                product_id,
                quantity,
                product_title,
                product_url,
                price_cents,
                price_currency,
                created_at,
                updated_at
            from unnest(@Orders)
            returning 
                id,
                order_id,
                product_id,
                quantity,
                product_title,
                product_url,
                price_cents,
                price_currency,
                created_at,
                updated_at;
        ";
        
        var conn = await unitOfWork.GetConnection(token);
        return (await conn.QueryAsync<V1OrderItemDal>(
            new CommandDefinition(sql, new {Orders = models}, cancellationToken: token))).ToArray();
    }

    public async Task<V1OrderItemDal[]> Query(QueryOrderItemsDalModel model, CancellationToken token)
    {
        var sql = new StringBuilder(@"
            select 
                id,
                order_id,
                product_id,
                quantity,
                product_title,
                product_url,
                price_cents,
                price_currency,
                created_at,
                updated_at
            from order_items
        ");
        
        var param = new DynamicParameters();
        var conditions = new List<string>();

        if (model.Ids?.Length > 0)
        {
            param.Add("Ids", model.Ids);
            conditions.Add("id = ANY(@Ids)");
        }
        
        if (model.OrderIds?.Length > 0)
        {
            param.Add("OrderIds", model.OrderIds);
            conditions.Add("order_id = ANY(@OrderIds)");
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
        var res = await conn.QueryAsync<V1OrderItemDal>(new CommandDefinition(
            sql.ToString(), param, cancellationToken: token));
        
        return res.ToArray();
    }
}