namespace GoDesk.Foundation.Verification;

public sealed class ArchitectureFacts
{
    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Foundation_runs_only_on_64_bit_windows()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.True(Environment.Is64BitProcess);
    }
#pragma warning restore CA1707
}
