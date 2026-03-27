namespace Auth.Api.Features.Auth.Register;

public sealed class RegisterUserCommandValidator
{
    public IReadOnlyCollection<string> Validate(RegisterUserCommand command)
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
