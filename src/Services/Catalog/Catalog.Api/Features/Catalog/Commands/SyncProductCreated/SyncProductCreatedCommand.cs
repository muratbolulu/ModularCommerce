using MediatR;
using Shared.Contracts.Events;

namespace Catalog.Api.Features.Catalog.Commands.SyncProductCreated;

public sealed record SyncProductCreatedCommand(ProductCreatedEvent Event) : IRequest;
