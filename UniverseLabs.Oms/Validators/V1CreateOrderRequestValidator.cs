using FluentValidation;
using UniverseLabs.Oms.Models.Dto.V1.Requests;

namespace UniverseLabs.Oms.Validators;

public class V1CreateOrderRequestValidator: AbstractValidator<V1CreateOrderRequest>
{
    public V1CreateOrderRequestValidator()
    {
        RuleFor(x => x.Orders)
            .NotEmpty();

        RuleForEach(x => x.Orders)
            .SetValidator(new OrderValidator())
            .When(x => x.Orders is not null);
    }
    
    public class OrderValidator: AbstractValidator<V1CreateOrderRequest.Order>
    {
        public OrderValidator()
        {
            RuleFor(x => x.CustomerId)
                .GreaterThan(0);

            RuleFor(x => x.DeliveryAddress)
                .NotEmpty();

            RuleFor(x => x.TotalPriceCents)
                .GreaterThan(0);

            RuleFor(x => x.TotalPriceCurrency)
                .NotEmpty();

            RuleFor(x => x.OrderItems)
                .NotEmpty();

            RuleForEach(x => x.OrderItems)
                .SetValidator(new OrderItemValidator())
                .When(x => x.OrderItems is not null);
            
            RuleFor(x => x)
                .Must(x => x.TotalPriceCents == x.OrderItems.Sum(y => y.PriceCents * y.Quantity))
                .When(x => x.OrderItems is not null)
                .WithMessage("TotalPriceCents should be equal to sum of all OrderItems.PriceCents * OrderItems.Quantity");

            RuleFor(x => x)
                .Must(x => x.OrderItems.Select(r => r.PriceCurrency).Distinct().Count() == 1)
                .When(x => x.OrderItems is not null)
                .WithMessage("All OrderItems.PriceCurrency should be the same");
            
            RuleFor(x => x)
                .Must(x => x.OrderItems.Select(r => r.PriceCurrency).First() == x.TotalPriceCurrency)
                .When(x => x.OrderItems is not null)
                .WithMessage("OrderItems.PriceCurrency should be the same as TotalPriceCurrency");
        }
    }
    
    public class OrderItemValidator: AbstractValidator<V1CreateOrderRequest.OrderItem>
    {
        public OrderItemValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0);

            RuleFor(x => x.PriceCents)
                .GreaterThan(0);

            RuleFor(x => x.PriceCurrency)
                .NotEmpty();

            RuleFor(x => x.ProductTitle)
                .NotEmpty();

            RuleFor(x => x.Quantity)
                .GreaterThan(0);
        }
    }
}