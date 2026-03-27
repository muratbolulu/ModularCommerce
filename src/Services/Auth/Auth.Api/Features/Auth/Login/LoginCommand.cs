using MediatR;
using Shared.Contracts.Auth;

namespace Auth.Api.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password, string ClientIp, string UserAgent) : IRequest<TokenResponse?>;
