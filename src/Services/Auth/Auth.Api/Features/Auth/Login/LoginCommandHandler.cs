using Auth.Api.Auth;
using Auth.Api.Persistence;
using Auth.Api.Persistence.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Auth;

namespace Auth.Api.Features.Auth.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, TokenResponse?>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly TokenService _tokenService;
    private readonly LoginCommandValidator _validator;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        AuthDbContext dbContext,
        TokenService tokenService,
        LoginCommandValidator validator,
        ILogger<LoginCommandHandler> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<TokenResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning(
                "Login failed for {Email} from {ClientIp} with user-agent {UserAgent}",
                request.Email,
                request.ClientIp,
                request.UserAgent);
            return null;
        }

        var tokenResponse = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        _dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = tokenResponse.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        _logger.LogInformation(
            "Login succeeded for {Email} (UserId: {UserId}, Roles: {Roles}) from {ClientIp} with user-agent {UserAgent}",
            request.Email,
            user.Id,
            string.Join(",", roles),
            request.ClientIp,
            request.UserAgent);

        return tokenResponse;
    }
}
