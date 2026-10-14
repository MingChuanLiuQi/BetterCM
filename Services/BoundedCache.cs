namespace BetterCM.Services;

// Bounded LRU with explicit expiry. Icon extraction stays on its existing STA worker.
internal sealed class BoundedCache<TKey, TValue>(int capacity, IEqualityComparer<TKey>? comparer = null) where TKey : notnull
{
    private readonly Dictionary<TKey, (TValue Value, DateTimeOffset Expires, LinkedListNode<TKey> Node)> _items = new(comparer);
    private readonly LinkedList<TKey> _order = new();
    internal int Count => _items.Count;
    internal bool TryGet(TKey key, DateTimeOffset now, out TValue value)
    {
        if (_items.TryGetValue(key, out var item))
        {
            if (item.Expires > now) { _order.Remove(item.Node); _order.AddLast(item.Node); value = item.Value; return true; }
            Remove(key);
        }
        value = default!; return false;
    }
    internal void Set(TKey key, TValue value, DateTimeOffset expires)
    {
        Remove(key);
        if (_items.Count >= capacity && _order.First is { } first) Remove(first.Value);
        _items.Add(key, (value, expires, _order.AddLast(key)));
    }
    private void Remove(TKey key) { if (_items.Remove(key, out var item)) _order.Remove(item.Node); }
}
