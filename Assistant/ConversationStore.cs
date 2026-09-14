using System.Collections.Concurrent;

namespace TaskAssistant.Mvc.Assistant;

/// <summary>
/// Minimal in-memory, process-local store of recent turns per conversationId, so a follow-up like
/// "actually make that due tomorrow" can be resolved without the client repeating itself.
/// Deliberately simple (bonus feature, not a core requirement) - not persisted, not distributed.
/// </summary>
public class ConversationStore
{
    private const int MaxTurnsPerConversation = 8;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private class Entry
    {
        public List<ConversationTurn> Turns { get; } = new();
        public DateTime LastAccessUtc { get; set; } = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<string, Entry> _conversations = new();

    public IReadOnlyList<ConversationTurn> GetHistory(string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return Array.Empty<ConversationTurn>();
        Evict();
        return _conversations.TryGetValue(conversationId, out var entry) ? entry.Turns.ToList() : Array.Empty<ConversationTurn>();
    }

    public void Append(string? conversationId, string userMessage, string modelSummary)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;

        var entry = _conversations.GetOrAdd(conversationId, _ => new Entry());
        entry.LastAccessUtc = DateTime.UtcNow;
        entry.Turns.Add(new ConversationTurn("user", userMessage));
        entry.Turns.Add(new ConversationTurn("model", modelSummary));

        while (entry.Turns.Count > MaxTurnsPerConversation)
        {
            entry.Turns.RemoveAt(0);
        }
    }

    private void Evict()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var (key, entry) in _conversations)
        {
            if (entry.LastAccessUtc < cutoff)
            {
                _conversations.TryRemove(key, out _);
            }
        }
    }
}
