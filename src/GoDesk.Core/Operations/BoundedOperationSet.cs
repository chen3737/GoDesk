namespace GoDesk.Core.Operations;

/// <summary>
/// Maintains a thread-safe window of the last successfully accepted unique operation IDs.
/// </summary>
/// <remarks>
/// Duplicate attempts do not refresh insertion age. Capacity is a trusted local configuration value.
/// </remarks>
public sealed class BoundedOperationSet
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Queue<OperationId> _insertionOrder = new();
    private readonly HashSet<OperationId> _known = new(new OperationIdEqualityComparer());

    public BoundedOperationSet(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>
    /// Accepts an unseen, non-empty operation ID without refreshing the age of duplicate entries.
    /// </summary>
    public bool TryAdd(OperationId operationId)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException("An operation ID cannot be empty.", nameof(operationId));
        }

        lock (_gate)
        {
            if (!_known.Add(operationId))
            {
                return false;
            }

            _insertionOrder.Enqueue(operationId);
            while (_known.Count > _capacity)
            {
                OperationId oldest = _insertionOrder.Dequeue();
                _known.Remove(oldest);
            }

            return true;
        }
    }

    private sealed class OperationIdEqualityComparer : IEqualityComparer<OperationId>
    {
        public bool Equals(OperationId x, OperationId y) => x.Value == y.Value;

        public int GetHashCode(OperationId operationId)
        {
            Span<byte> bytes = stackalloc byte[16];
            _ = operationId.Value.TryWriteBytes(bytes);

            // Mix every byte with HashCode's process-randomized seed so hostile GUIDs cannot reuse
            // Guid.GetHashCode's deterministic XOR collisions against the locked set.
            var hashCode = new HashCode();
            hashCode.AddBytes(bytes);
            return hashCode.ToHashCode();
        }
    }
}
