using System.Collections.Concurrent;

namespace Crypto_Payment.Services;

public class PresenceTracker
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _onlineUsers = new();

    public Task UserConnected(string userId, string connectionId)
    {
        _onlineUsers.AddOrUpdate(userId,
            _ => new HashSet<string> { connectionId },
            (_, set) => { lock (set) { set.Add(connectionId); } return set; });
        return Task.CompletedTask;
    }

    public Task<bool> UserDisconnected(string userId, string connectionId)
    {
        if (!_onlineUsers.TryGetValue(userId, out var connections))
            return Task.FromResult(false);

        lock (connections)
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _onlineUsers.TryRemove(userId, out _);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public List<string> GetOnlineUsers()
    {
        return _onlineUsers.Keys.ToList();
    }

    public bool IsOnline(string userId)
    {
        return _onlineUsers.ContainsKey(userId);
    }

    public List<string> GetConnections(string userId)
    {
        return _onlineUsers.TryGetValue(userId, out var connections)
            ? connections.ToList()
            : new List<string>();
    }
}
