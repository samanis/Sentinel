namespace Sentinel.UnitTests;

public sealed class SolutionStructureTests
{
    [Fact]
    public void ExpectedAssembliesAreAvailable()
    {
        Assert.NotNull(typeof(Domain.AssemblyMarker).Assembly);
        Assert.NotNull(typeof(Application.AssemblyMarker).Assembly);
    }
}
