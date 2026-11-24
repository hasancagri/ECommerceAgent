using Catalog.Api.Search;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 026 (İlke VI test-first): pgvector text-literal kurucu. InvariantGlobalization → ondalık hep "." .
public class VectorLiteralTests
{
    [Fact]
    public void From_Empty_ReturnsBrackets()
    {
        VectorLiteral.From([]).ShouldBe("[]");
    }

    [Fact]
    public void From_SingleValue_WrapsOne()
    {
        VectorLiteral.From([1.5f]).ShouldBe("[1.5]");
    }

    [Fact]
    public void From_MultipleValues_CommaSeparated()
    {
        VectorLiteral.From([1f, 2.5f, -3f]).ShouldBe("[1,2.5,-3]");
    }

    [Fact]
    public void From_Fraction_UsesDotDecimal()
    {
        VectorLiteral.From([0.285f]).ShouldBe("[0.285]");
    }
}
