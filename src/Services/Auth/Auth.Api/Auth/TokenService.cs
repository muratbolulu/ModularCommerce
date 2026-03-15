using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Shared.Contracts.Auth;

namespace Auth.Api.Auth;

public sealed class TokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public TokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public async Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var issuer = _configuration["JWT_ISSUER"] ?? "modular-commerce";
        var audience = _configuration["JWT_AUDIENCE"] ?? "modular-commerce-clients";
        var signingKey = _configuration["JWT_SIGNING_KEY"] ?? "development-signing-key-please-change";
        var expires = DateTime.UtcNow.AddMinutes(20);

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(jwtToken),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            expires);
    }
}
