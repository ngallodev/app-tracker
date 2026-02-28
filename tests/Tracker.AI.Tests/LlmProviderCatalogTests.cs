using Tracker.AI.Cli;
using Xunit;

namespace Tracker.AI.Tests;

public sealed class LlmProviderCatalogTests
{
    [Fact]
    public void Lmstudio_IsSupported()
    {
        Assert.True(LlmProviderCatalog.IsSupported(LlmProviderCatalog.Lmstudio));
    }
}
