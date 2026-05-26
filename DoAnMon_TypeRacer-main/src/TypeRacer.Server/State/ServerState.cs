using System.Collections.Concurrent;

namespace TypeRacer.Server.State;

public class ServerState
{
    // Key: session token → Connected client
    public ConcurrentDictionary<string, ConnectedClient> Clients { get; } = new();

    // Key: room code → Game room
    public ConcurrentDictionary<string, GameRoom> Rooms { get; } = new();

    public ConnectedClient? GetClientByToken(string sessionToken)
    {
        Clients.TryGetValue(sessionToken, out var client);
        return client;
    }

    public ConnectedClient? GetClientByUserId(int userId)
    {
        return Clients.Values.FirstOrDefault(c => c.UserId == userId);
    }

    public void AddClient(string sessionToken, ConnectedClient client)
    {
        Clients[sessionToken] = client;
    }

    public void RemoveClient(string sessionToken)
    {
        Clients.TryRemove(sessionToken, out _);
    }

    public GameRoom? GetRoom(string roomCode)
    {
        Rooms.TryGetValue(roomCode, out var room);
        return room;
    }

    public void AddRoom(GameRoom room)
    {
        Rooms[room.RoomCode] = room;
    }

    public void RemoveRoom(string roomCode)
    {
        Rooms.TryRemove(roomCode, out _);
    }

    public List<GameRoom> GetAvailableRooms()
    {
        return Rooms.Values
            .Where(r => r.Status == Shared.Enums.RoomStatus.Waiting || r.AllowJoinInProgress)
            .OrderByDescending(r => r.IsCommunityRoom)
            .ThenBy(r => r.RoomCode)
            .ToList();
    }
}
