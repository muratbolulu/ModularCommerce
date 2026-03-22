using Auth.Api.Auth;
using Auth.Api.Persistence;
using Auth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Auth;

namespace Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly TokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        AuthDbContext dbContext,
        TokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("User registration failed for {Email}", request.Email);
            return BadRequest(result.Errors.Select(x => x.Description));
        }

        await _userManager.AddToRoleAsync(user, "admin");
        _logger.LogInformation("User {Email} registered successfully with default admin role", request.Email);
        return Created($"/auth/users/{user.Id}", new { user.Id, user.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning(
                "Login failed for {Email} from {ClientIp} with user-agent {UserAgent}",
                request.Email,
                clientIp,
                userAgent);
            return Unauthorized();
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
            clientIp,
            userAgent);
        return Ok(tokenResponse);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken && !x.IsRevoked, cancellationToken);

        if (refreshToken is null || refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token validation failed");
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(refreshToken.UserId);
        if (user is null)
        {
            _logger.LogWarning("Refresh token belongs to a missing user");
            return Unauthorized();
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
        return Ok(newTokens);
    }
}
