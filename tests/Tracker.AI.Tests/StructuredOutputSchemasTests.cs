using System.Text.Json;
using Tracker.AI;
using Tracker.AI.Models;
using Xunit;

namespace Tracker.AI.Tests;

public sealed class StructuredOutputSchemasTests
{
    [Fact]
    public void JdExtraction_Schema_IsPresent_AndStrict()
    {
        var schema = StructuredOutputSchemas.GetForType<JdExtraction>();

        Assert.NotNull(schema);
        Assert.Equal("jd_extraction", schema!.Name);

        using var doc = JsonDocument.Parse(schema.SchemaJson);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("properties").TryGetProperty("role_title", out _));
    }

    [Fact]
    public void GapAnalysis_Schema_IsPresent_AndStrict()
    {
        var schema = StructuredOutputSchemas.GetForType<GapAnalysis>();

        Assert.NotNull(schema);
        Assert.Equal("gap_analysis", schema!.Name);

        using var doc = JsonDocument.Parse(schema.SchemaJson);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("properties").TryGetProperty("matches", out _));
    }
}
