namespace UniverseLabs.Oms.Models.Dto.V1.Requests;

public class V1QueryOrdersRequest
{
    public long[] Ids { get; set; }

    public long[] CustomerIds { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
    
    public bool IncludeOrderItems { get; set; }
}