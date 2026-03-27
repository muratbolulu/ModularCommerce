using Auth.Api.Persistence.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Auth.Api.Features.Auth.Register;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RegisterUserCommandValidator _validator;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        UserManager<ApplicationUser> userManager,
        RegisterUserCommandValidator validator,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userManager = userManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("User registration failed for {Email}", request.Email);
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        await _userManager.AddToRoleAsync(user, "admin");
        _logger.LogInformation("User {Email} registered successfully with default admin role", request.Email);
        return new RegisterUserResult(user.Id, user.Email ?? string.Empty);
    }
}
