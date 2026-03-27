using Auth.Api.Features.Auth.Login;
using Auth.Api.Features.Auth.Refresh;
using Auth.Api.Features.Auth.Register;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Auth;

namespace Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ISender sender, ILogger<AuthController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new RegisterUserCommand(request.Email, request.Password), cancellationToken);
            return Created($"/auth/users/{result.Id}", new { result.Id, result.Email });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Register request failed for {Email}", request.Email);
            return BadRequest(new { ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            var result = await _sender.Send(new LoginCommand(request.Email, request.Password, clientIp, userAgent), cancellationToken);
            return result is null ? Unauthorized() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Login request validation failed for {Email}", request.Email);
            return BadRequest(new { ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
            return result is null ? Unauthorized() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Refresh request validation failed");
            return BadRequest(new { ex.Message });
        }
    }
}
