using ScoutAgent.Api.Agents;
using ScoutAgent.Api.Options;
using Shouldly;
using Xunit;

namespace ScoutAgent.Api.Tests;

// 019: agent-başına model çözümleme (saf — İlke VI).
public class ModelResolutionTests
{
    [Fact]
    public void ResolveModel_KeyInAgentsMap_ReturnsMappedModel()
    {
        var options = new ScoutOptions { Model = "gpt-4o-mini", Agents = { ["library"] = "gpt-4o" } };

        ScoutModelResolver.Resolve(options, "library").ShouldBe("gpt-4o");
    }

    [Fact]
    public void ResolveModel_KeyMissing_FallsBackToDefault()
    {
        var options = new ScoutOptions { Model = "gpt-4o-mini" };

        ScoutModelResolver.Resolve(options, "catalog").ShouldBe("gpt-4o-mini");
    }

    [Fact]
    public void ResolveModel_EmptyMappedValue_FallsBackToDefault()
    {
        var options = new ScoutOptions { Model = "gpt-4o-mini", Agents = { ["catalog"] = "  " } };

        ScoutModelResolver.Resolve(options, "catalog").ShouldBe("gpt-4o-mini");
    }

    [Fact]
    public void ResolveRouterModel_RouterModelSet_ReturnsRouterModel()
    {
        var options = new ScoutOptions { Model = "gpt-4o", RouterModel = "gpt-4o-mini" };

        ScoutModelResolver.ResolveRouter(options).ShouldBe("gpt-4o-mini");
    }

    [Fact]
    public void ResolveRouterModel_RouterModelUnset_FallsBackToDefault()
    {
        var options = new ScoutOptions { Model = "gpt-4o-mini" };

        ScoutModelResolver.ResolveRouter(options).ShouldBe("gpt-4o-mini");
    }
}
