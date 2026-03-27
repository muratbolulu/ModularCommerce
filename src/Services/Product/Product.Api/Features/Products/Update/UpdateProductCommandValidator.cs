namespace Product.Api.Features.Products.Update;

public sealed class UpdateProductCommandValidator
{
    public IReadOnlyCollection<string> Validate(UpdateProductCommand command)
    {
        var errors = new List<string>();
        if (command.Id == Guid.Empty)
        {
            errors.Add("Product id is required.");
        }

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
