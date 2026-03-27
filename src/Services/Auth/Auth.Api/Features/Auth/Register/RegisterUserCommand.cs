using MediatR;

namespace Auth.Api.Features.Auth.Register;

public sealed record RegisterUserCommand(string Email, string Password) : IRequest<RegisterUserResult>;
