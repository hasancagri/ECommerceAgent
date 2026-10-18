namespace Catalog.Api.Tests;

// 043 T005: Product.SetSpecifications — tam-değiştirme + mükerrer attribute guard'ı.
public class ProductSpecificationTests
{
    private static Product NewProduct() =>
        Product.Create("Ürün", "SKU-1", ProductType.Simple, Money.Create(10m)!, "kısa", "tam").Data!;

    private static readonly Guid AttrRenk = Guid.NewGuid();
    private static readonly Guid AttrMateryal = Guid.NewGuid();
    private static readonly Guid OptSiyah = Guid.NewGuid();
    private static readonly Guid OptCelik = Guid.NewGuid();

    [Fact]
    public void SetSpecifications_AssignsList()
    {
        var product = NewProduct();

        var result = product.SetSpecifications(
        [
            ProductSpecificationAssignment.Create(AttrRenk, OptSiyah),
            ProductSpecificationAssignment.Create(AttrMateryal, OptCelik),
        ]);

        result.IsSuccess.ShouldBeTrue();
        product.Specifications.Count.ShouldBe(2);
        product.Specifications[0].AttributeId.ShouldBe(AttrRenk);
        product.Specifications[0].OptionId.ShouldBe(OptSiyah);
    }

    [Fact]
    public void SetSpecifications_ReplacesExisting()
    {
        var product = NewProduct();
        product.SetSpecifications([ProductSpecificationAssignment.Create(AttrRenk, OptSiyah)]);

        var result = product.SetSpecifications(
            [ProductSpecificationAssignment.Create(AttrMateryal, OptCelik)]);

        result.IsSuccess.ShouldBeTrue();
        product.Specifications.Count.ShouldBe(1);
        product.Specifications[0].AttributeId.ShouldBe(AttrMateryal);
    }

    [Fact]
    public void SetSpecifications_EmptyList_Clears()
    {
        var product = NewProduct();
        product.SetSpecifications([ProductSpecificationAssignment.Create(AttrRenk, OptSiyah)]);

        var result = product.SetSpecifications([]);

        result.IsSuccess.ShouldBeTrue();
        product.Specifications.ShouldBeEmpty();
    }

    [Fact]
    public void SetSpecifications_DuplicateAttribute_ReturnsErrorAndKeepsOld()
    {
        var product = NewProduct();
        product.SetSpecifications([ProductSpecificationAssignment.Create(AttrMateryal, OptCelik)]);

        var result = product.SetSpecifications(
        [
            ProductSpecificationAssignment.Create(AttrRenk, OptSiyah),
            ProductSpecificationAssignment.Create(AttrRenk, OptCelik),
        ]);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == CatalogResourceConstants.SPEC_DUPLICATE_ATTRIBUTE);
        // hatalı çağrı mevcut atamaları BOZMAZ
        product.Specifications.Count.ShouldBe(1);
        product.Specifications[0].AttributeId.ShouldBe(AttrMateryal);
    }
}
