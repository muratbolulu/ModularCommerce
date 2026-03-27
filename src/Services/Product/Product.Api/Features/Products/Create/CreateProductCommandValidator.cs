namespace Product.Api.Features.Products.Create;

public sealed class CreateProductCommandValidator
{
    public IReadOnlyCollection<string> Validate(CreateProductCommand command)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors.Add("Product name is required.");
        }

        if (command.Price < 0)
        {
            errors.Add("Price cannot be negative.");
        }

        if (command.Stock < 0)
        {
            errors.Add("Stock cannot be negative.");
        }

        return errors;
    }
}
