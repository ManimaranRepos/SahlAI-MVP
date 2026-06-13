using System.Collections.Concurrent;

namespace SahlAI.Api.Services;

public record ChatTurn(string Role, string Content);

/// <summary>
/// Stores recent conversation history per user (keyed by WhatsApp phone number).
/// In-memory for the MVP — swap for Redis / SQL Server in production so history
/// survives restarts and scales across instances.
/// </summary>
public interface IConversationStore
{
    IReadOnlyList<ChatTurn> GetHistory(string userId);
    void Append(string userId, string role, string content);
    void Clear(string userId);
}

public class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, List<ChatTurn>> _store = new();
    private const int MaxTurns = 20; // hard cap to bound memory

    public IReadOnlyList<ChatTurn> GetHistory(string userId) =>
        _store.TryGetValue(userId, out var list) ? list.ToList() : new List<ChatTurn>();

    public void Append(string userId, string role, string content)
    {
        var list = _store.GetOrAdd(userId, _ => new List<ChatTurn>());
        lock (list)
        {
            list.Add(new ChatTurn(role, content));
            if (list.Count > MaxTurns) list.RemoveRange(0, list.Count - MaxTurns);
        }
    }

    public void Clear(string userId) => _store.TryRemove(userId, out _);
}
