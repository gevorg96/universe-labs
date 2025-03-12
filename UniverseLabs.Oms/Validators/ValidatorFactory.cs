using FluentValidation;

namespace UniverseLabs.Oms.Validators;

public class ValidatorFactory(IServiceProvider serviceProvider)
{
    public IValidator<T> GetValidator<T>()
    {
        return serviceProvider.GetRequiredService<IValidator<T>>()!;
    }
}