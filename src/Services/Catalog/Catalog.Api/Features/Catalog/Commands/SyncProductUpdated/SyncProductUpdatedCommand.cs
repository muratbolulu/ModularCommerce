using MediatR;
using Shared.Contracts.Events;

namespace Catalog.Api.Features.Catalog.Commands.SyncProductUpdated;

public sealed record SyncProductUpdatedCommand(ProductUpdatedEvent Event) : IRequest;
