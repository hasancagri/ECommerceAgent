using Catalog.Api.Search;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 018 (İlke VI test-first): birleşik arama mod kararı saf fonksiyon. Metin+sort kombinasyonlarından
// tek mod türetir (Relevance / FieldSort / DefaultBrowse). Tek mod — alaka + alan-sıralaması birlikte YOK.
public class SearchModeResolutionTests
{
    [Fact]
    public void Resolve_TextWithoutSort_ReturnsRelevance()
    {
        var mode = SearchModeResolver.Resolve(hasText: true, sort: null);
        mode.ShouldBe(SearchMode.Relevance);
    }

    [Fact]
    public void Resolve_SortWithoutText_ReturnsFieldSort()
    {
        var mode = SearchModeResolver.Resolve(hasText: false, sort: SortOption.PriceAsc);
        mode.ShouldBe(SearchMode.FieldSort);
    }

    [Fact]
    public void Resolve_SortWithText_ReturnsFieldSort()
    {
        // Sort verilince alaka EZİLİR (FR-004: tek mod).
        var mode = SearchModeResolver.Resolve(hasText: true, sort: SortOption.Newest);
        mode.ShouldBe(SearchMode.FieldSort);
    }

    [Fact]
    public void Resolve_NoTextNoSort_ReturnsDefaultBrowse()
    {
        var mode = SearchModeResolver.Resolve(hasText: false, sort: null);
        mode.ShouldBe(SearchMode.DefaultBrowse);
    }
}
