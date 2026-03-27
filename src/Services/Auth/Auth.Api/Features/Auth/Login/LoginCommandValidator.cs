namespace Auth.Api.Features.Auth.Login;

public sealed class LoginCommandValidator
{
    public IReadOnlyCollection<string> Validate(LoginCommand command)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            errors.Add("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            errors.Add("Password is required.");
        }

        return errors;
    }
}
