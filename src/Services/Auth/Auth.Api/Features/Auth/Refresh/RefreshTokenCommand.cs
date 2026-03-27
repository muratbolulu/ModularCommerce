using MediatR;
using Shared.Contracts.Auth;

namespace Auth.Api.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<TokenResponse?>;
