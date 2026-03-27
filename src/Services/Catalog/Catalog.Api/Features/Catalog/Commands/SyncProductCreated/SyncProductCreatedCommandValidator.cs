namespace Catalog.Api.Features.Catalog.Commands.SyncProductCreated;

public sealed class SyncProductCreatedCommandValidator
{
    public IReadOnlyCollection<string> Validate(SyncProductCreatedCommand command)
    {
        var errors = new List<string>();
        if (command.Event.ProductId == Guid.Empty)
        {
            errors.Add("ProductId is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Event.Name))
        {
            errors.Add("Product name is required.");
        }

        if (command.Event.Price < 0)
        {
            errors.Add("Price cannot be negative.");
        }

        return errors;
    }
}
