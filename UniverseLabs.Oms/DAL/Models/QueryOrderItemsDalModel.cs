namespace UniverseLabs.Oms.DAL.Models;

public class QueryOrderItemsDalModel
{
    public long[] Ids { get; set; }

    public long[] OrderIds { get; set; }

    public int Limit { get; set; }

    public int Offset { get; set; }
}