using System.Diagnostics;
using System.Reflection;
using GoDesk.Core.Operations;

namespace GoDesk.Foundation.Verification.Core;

public sealed class BoundedOperationSetTests
{
    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Duplicate_operation_is_rejected()
    {
        const int AttemptCount = 16;
        var set = new BoundedOperationSet(2);
        var operationId = new OperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var results = new bool[AttemptCount];
        var errors = new Exception?[AttemptCount];
        using var ready = new CountdownEvent(AttemptCount);
        using var start = new ManualResetEventSlim();
        Thread[] workers = Enumerable.Range(0, AttemptCount)
            .Select(index => new Thread(() =>
            {
                ready.Signal();
                start.Wait();

                try
                {
                    results[index] = set.TryAdd(operationId);
                }
                catch (Exception exception)
                {
                    errors[index] = exception;
                }
            }))
            .ToArray();

        foreach (Thread worker in workers)
        {
            worker.IsBackground = true;
            worker.Start();
        }

        bool allWorkersReady;
        try
        {
            allWorkersReady = ready.Wait(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            start.Set();
        }
        TimeSpan joinTimeout = TimeSpan.FromSeconds(10);
        var joinTimer = Stopwatch.StartNew();
        bool[] allWorkersJoined = workers
            .Select(worker =>
            {
                TimeSpan remaining = joinTimeout - joinTimer.Elapsed;
                return remaining > TimeSpan.Zero && worker.Join(remaining);
            })
            .ToArray();

        Assert.True(allWorkersReady);
        Assert.All(allWorkersJoined, Assert.True);
        Assert.All(errors, Assert.Null);
        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(AttemptCount - 1, results.Count(result => !result));

        OperationId generated = OperationId.New();
        Assert.NotEqual(Guid.Empty, generated.Value);
        Assert.Equal(generated.Value.ToString("D"), generated.ToString());

        FieldInfo? knownField = typeof(BoundedOperationSet).GetField(
            "_known",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(knownField);
        var known = Assert.IsType<HashSet<OperationId>>(knownField.GetValue(set));
        Assert.NotSame(EqualityComparer<OperationId>.Default, known.Comparer);

        var firstComparedId = new OperationId(
            Guid.Parse("00000000-0000-0000-0100-000000000000"));
        var secondComparedId = new OperationId(
            Guid.Parse("00010000-0001-0000-0100-000000000000"));
        Assert.NotEqual(firstComparedId, secondComparedId);
        Assert.True(known.Comparer.Equals(firstComparedId, firstComparedId));
        Assert.False(known.Comparer.Equals(firstComparedId, secondComparedId));
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Oldest_operation_is_evicted_at_capacity()
    {
        var set = new BoundedOperationSet(2);
        var first = new OperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = new OperationId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var third = new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333"));

        Assert.True(set.TryAdd(first));
        Assert.True(set.TryAdd(second));
        Assert.False(set.TryAdd(first));
        Assert.True(set.TryAdd(third));
        Assert.True(set.TryAdd(first));

        var singleEntrySet = new BoundedOperationSet(1);
        Assert.True(singleEntrySet.TryAdd(first));
        Assert.True(singleEntrySet.TryAdd(second));
        Assert.True(singleEntrySet.TryAdd(first));
        Assert.False(singleEntrySet.TryAdd(first));

        const int UniqueOperationCount = 64;
        var concurrentSet = new BoundedOperationSet(UniqueOperationCount);
        OperationId[] uniqueOperationIds = Enumerable.Range(1, UniqueOperationCount)
            .Select(value => new OperationId(
                Guid.Parse($"00000000-0000-0000-0000-{value:D12}")))
            .ToArray();
        var concurrentResults = new bool[UniqueOperationCount];
        var concurrentErrors = new Exception?[UniqueOperationCount];
        using var ready = new CountdownEvent(UniqueOperationCount);
        using var start = new ManualResetEventSlim();
        Thread[] workers = Enumerable.Range(0, UniqueOperationCount)
            .Select(index => new Thread(() =>
            {
                ready.Signal();
                start.Wait();

                try
                {
                    concurrentResults[index] = concurrentSet.TryAdd(uniqueOperationIds[index]);
                }
                catch (Exception exception)
                {
                    concurrentErrors[index] = exception;
                }
            }))
            .ToArray();

        foreach (Thread worker in workers)
        {
            worker.IsBackground = true;
            worker.Start();
        }

        bool allWorkersReady;
        try
        {
            allWorkersReady = ready.Wait(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            start.Set();
        }
        TimeSpan joinTimeout = TimeSpan.FromSeconds(10);
        var joinTimer = Stopwatch.StartNew();
        bool[] allWorkersJoined = workers
            .Select(worker =>
            {
                TimeSpan remaining = joinTimeout - joinTimer.Elapsed;
                return remaining > TimeSpan.Zero && worker.Join(remaining);
            })
            .ToArray();

        Assert.True(allWorkersReady);
        Assert.All(allWorkersJoined, Assert.True);
        Assert.All(concurrentErrors, Assert.Null);
        Assert.All(concurrentResults, Assert.True);
        Assert.All(uniqueOperationIds, operationId => Assert.False(concurrentSet.TryAdd(operationId)));
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Capacity_must_be_positive()
    {
        ArgumentOutOfRangeException zeroCapacityException = Assert.Throws<ArgumentOutOfRangeException>(
            () => new BoundedOperationSet(0));
        ArgumentOutOfRangeException negativeCapacityException = Assert.Throws<ArgumentOutOfRangeException>(
            () => new BoundedOperationSet(-1));
        Assert.Equal("capacity", zeroCapacityException.ParamName);
        Assert.Equal("capacity", negativeCapacityException.ParamName);

        var set = new BoundedOperationSet(1);
        ArgumentException emptyOperationIdException = Assert.Throws<ArgumentException>(
            () => set.TryAdd(new OperationId(Guid.Empty)));
        Assert.Equal("operationId", emptyOperationIdException.ParamName);
        Assert.StartsWith(
            "An operation ID cannot be empty.",
            emptyOperationIdException.Message,
            StringComparison.Ordinal);
    }
#pragma warning restore CA1707
}
