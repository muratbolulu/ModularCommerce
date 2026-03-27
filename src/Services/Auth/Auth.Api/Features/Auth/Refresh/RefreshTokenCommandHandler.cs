using Auth.Api.Auth;
using Auth.Api.Persistence;
using Auth.Api.Persistence.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Auth;

namespace Auth.Api.Features.Auth.Refresh;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenResponse?>
{
    private readonly AuthDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;
    private readonly RefreshTokenCommandValidator _validator;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        TokenService tokenService,
        RefreshTokenCommandValidator validator,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _tokenService = tokenService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<TokenResponse?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken && !x.IsRevoked, cancellationToken);

        if (refreshToken is null || refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token validation failed");
            return null;
        }

        var user = await _userManager.FindByIdAsync(refreshToken.UserId);
        if (user is null)
        {
            _logger.LogWarning("Refresh token belongs to a missing user");
            return null;
        }

        refreshToken.IsRevoked = true;
        var newTokens = await _tokenService.GenerateTokensAsync(user, cancellationToken);
        _dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newTokens.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Refresh token succeeded for user {UserId}", user.Id);
        return newTokens;
    }
}
