using UniverseLabs.Oms.Models.Dto.Common;

namespace UniverseLabs.Oms.Models.Dto.V1.Responses;

public class V1CreateOrderResponse
{
    public OrderUnit[] Orders { get; set; }
}