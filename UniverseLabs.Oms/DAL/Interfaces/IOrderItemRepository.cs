using UniverseLabs.Oms.DAL.Models;

namespace UniverseLabs.Oms.DAL.Interfaces;

public interface IOrderItemRepository
{
    Task<V1OrderItemDal[]> BulkInsert(V1OrderItemDal[] models, CancellationToken token);

    Task<V1OrderItemDal[]> Query(QueryOrderItemsDalModel model, CancellationToken token);
}