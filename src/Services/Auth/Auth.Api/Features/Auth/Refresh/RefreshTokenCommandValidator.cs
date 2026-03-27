namespace Auth.Api.Features.Auth.Refresh;

public sealed class RefreshTokenCommandValidator
{
    public IReadOnlyCollection<string> Validate(RefreshTokenCommand command)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            errors.Add("Refresh token is required.");
        }

        return errors;
    }
}
