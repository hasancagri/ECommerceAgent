namespace Basket.Api.Tests;

public class BasketTests
{
    private static BasketItem Item(decimal price, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "product", null, price);

    [Fact]
    public void Create_InitializesEmptyBasket()
    {
        var userId = Guid.NewGuid();

        var basket = BasketAggregate.Create(userId);

        basket.UserId.ShouldBe(userId);
        basket.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_AddsItemToBasket()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());

        basket.AddItem(Item(100m));

        basket.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void AddItem_WithSameId_ReplacesExistingItem()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.AddItem(Item(100m, id));

        basket.AddItem(Item(250m, id));

        basket.Items.Count.ShouldBe(1);
        basket.Items[0].Price.ShouldBe(250m);
    }

    [Fact]
    public void GetTotalPrice_SumsItemPrices()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));
        basket.AddItem(Item(50m));

        basket.GetTotalPrice().ShouldBe(150m);
    }

    [Fact]
    public void RemoveItem_ExistingItem_RemovesItAndReturnsOk()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.AddItem(Item(100m, id));

        var result = basket.RemoveItem(id);

        result.IsSuccess.ShouldBeTrue();
        basket.Items.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveItem_NonExistingItem_ReturnsNotFound()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));

        var result = basket.RemoveItem(Guid.NewGuid());

        result.IsSuccess.ShouldBeFalse();
        basket.Items.Count.ShouldBe(1);
    }

    // --- 012-stock-reservation: Quantity ---

    [Fact]
    public void SetItem_AddsNewItem_WithQuantity()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();

        basket.SetItem(id, "product", null, 100m, 3);

        basket.Items.Count.ShouldBe(1);
        basket.Items[0].Quantity.ShouldBe(3);
    }

    [Fact]
    public void SetItem_OnExistingItem_UpdatesQuantity_NoDuplicate()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.SetItem(id, "product", null, 100m, 1);

        basket.SetItem(id, "product", null, 100m, 4);

        basket.Items.Count.ShouldBe(1);
        basket.Items[0].Quantity.ShouldBe(4);
    }

    // 021: sabit ust sinir 5'tir (tek otorite; hem yazma reddi hem UI-siniri buradan turer).
    [Fact]
    public void MaxItemQuantity_Is5()
    {
        BasketAggregate.MaxItemQuantity.ShouldBe(5);
    }

    [Fact]
    public void GetItemQuantity_ReturnsQuantity_OrZeroWhenAbsent()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.SetItem(id, "product", null, 100m, 2);

        basket.GetItemQuantity(id).ShouldBe(2);
        basket.GetItemQuantity(Guid.NewGuid()).ShouldBe(0);
    }

    [Fact]
    public void GetTotalPrice_MultipliesByQuantity()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.SetItem(Guid.NewGuid(), "a", null, 100m, 2);
        basket.SetItem(Guid.NewGuid(), "b", null, 50m, 3);

        basket.GetTotalPrice().ShouldBe(100m * 2 + 50m * 3);
    }

    // --- 056: kalici sepet — sure/rezervasyon kavrami yok ---

    // Sepet zamana bagli hicbir uye tasimaz; yasam dongusu yalniz kullanici eylemi + checkout temizligi.
    [Fact]
    public void Basket_HasNoTimeBasedMembers()
    {
        var members = typeof(BasketAggregate).GetMembers()
            .Select(m => m.Name)
            .Where(n => n.Contains("Reservation", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Expire", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Purge", StringComparison.OrdinalIgnoreCase));

        members.ShouldBeEmpty();
    }

    // --- 057: MergeFrom — anonim sepet login'de hesaba tasinir ---

    [Fact]
    public void MergeFrom_MovesAllItems_IntoEmptyBasket()
    {
        var userBasket = BasketAggregate.Create(Guid.NewGuid());
        var anonymousBasket = BasketAggregate.Create(Guid.NewGuid());
        anonymousBasket.SetItem(Guid.NewGuid(), "a", null, 100m, 2);
        anonymousBasket.SetItem(Guid.NewGuid(), "b", null, 50m, 1);

        var result = userBasket.MergeFrom(anonymousBasket);

        result.IsSuccess.ShouldBeTrue();
        userBasket.Items.Count.ShouldBe(2);
        userBasket.GetTotalPrice().ShouldBe(100m * 2 + 50m);
    }

    [Fact]
    public void MergeFrom_SameProduct_SumsQuantities()
    {
        var id = Guid.NewGuid();
        var userBasket = BasketAggregate.Create(Guid.NewGuid());
        userBasket.SetItem(id, "product", null, 100m, 2);
        var anonymousBasket = BasketAggregate.Create(Guid.NewGuid());
        anonymousBasket.SetItem(id, "product", null, 100m, 1);

        userBasket.MergeFrom(anonymousBasket);

        userBasket.Items.Count.ShouldBe(1);
        userBasket.GetItemQuantity(id).ShouldBe(3);
    }

    [Fact]
    public void MergeFrom_SameProduct_CapsAtMaxItemQuantity()
    {
        var id = Guid.NewGuid();
        var userBasket = BasketAggregate.Create(Guid.NewGuid());
        userBasket.SetItem(id, "product", null, 100m, 4);
        var anonymousBasket = BasketAggregate.Create(Guid.NewGuid());
        anonymousBasket.SetItem(id, "product", null, 100m, 3);

        userBasket.MergeFrom(anonymousBasket);

        userBasket.GetItemQuantity(id).ShouldBe(BasketAggregate.MaxItemQuantity);
    }

    [Fact]
    public void MergeFrom_IncomingOverMax_CapsEvenIntoEmptyBasket()
    {
        var id = Guid.NewGuid();
        var userBasket = BasketAggregate.Create(Guid.NewGuid());
        var anonymousBasket = BasketAggregate.Create(Guid.NewGuid());
        anonymousBasket.SetItem(id, "product", null, 100m, BasketAggregate.MaxItemQuantity + 2);

        userBasket.MergeFrom(anonymousBasket);

        userBasket.GetItemQuantity(id).ShouldBe(BasketAggregate.MaxItemQuantity);
    }

    [Fact]
    public void MergeFrom_EmptyOther_IsNoOp()
    {
        var id = Guid.NewGuid();
        var userBasket = BasketAggregate.Create(Guid.NewGuid());
        userBasket.SetItem(id, "product", null, 100m, 2);
        var anonymousBasket = BasketAggregate.Create(Guid.NewGuid());

        var result = userBasket.MergeFrom(anonymousBasket);

        result.IsSuccess.ShouldBeTrue();
        userBasket.Items.Count.ShouldBe(1);
        userBasket.GetItemQuantity(id).ShouldBe(2);
    }

    [Fact]
    public void MergeFrom_KeepsExistingItems_NotInOther()
    {
        var keptId = Guid.NewGuid();
        var userBasket = BasketAggregate.Create(Guid.NewGuid());
        userBasket.SetItem(keptId, "kept", null, 80m, 1);
        var anonymousBasket = BasketAggregate.Create(Guid.NewGuid());
        anonymousBasket.SetItem(Guid.NewGuid(), "incoming", null, 100m, 1);

        userBasket.MergeFrom(anonymousBasket);

        userBasket.Items.Count.ShouldBe(2);
        userBasket.GetItemQuantity(keptId).ShouldBe(1);
    }

    [Fact]
    public void RemoveItem_LastItem_LeavesEmptyPersistentBasket()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.SetItem(id, "product", null, 100m, 1);

        basket.RemoveItem(id);

        basket.Items.ShouldBeEmpty();
        basket.GetTotalPrice().ShouldBe(0m);
    }
}