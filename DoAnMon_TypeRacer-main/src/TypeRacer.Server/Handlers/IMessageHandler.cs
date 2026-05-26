using TypeRacer.Server.Network;
using TypeRacer.Server.State;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Handlers;

public interface IMessageHandler
{
    Task HandleAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct);
}
