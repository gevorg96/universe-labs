using FluentValidation;
using UniverseLabs.Oms.Models.Dto.V1.Requests;

namespace UniverseLabs.Oms.Validators;

public class V1QueryOrdersRequestValidator: AbstractValidator<V1QueryOrdersRequest>
{
    public V1QueryOrdersRequestValidator()
    {
        RuleForEach(x => x.Ids)
            .GreaterThan(0);

        RuleForEach(x => x.CustomerIds)
            .GreaterThan(0);

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .When(x => x.Page is not null);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .When(x => x.PageSize is not null);

        RuleFor(x => x)
            .Must(x => x.Ids?.Length > 0 || x.CustomerIds?.Length > 0)
            .WithMessage("At least one of Ids or CustomerIds must be specified");
    }
}