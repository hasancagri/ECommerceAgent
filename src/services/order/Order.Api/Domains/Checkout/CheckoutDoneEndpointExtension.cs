namespace Order.Api.Domains.Checkout;

// 094: PG hosted ödemeden sonra tarayıcı buraya döner (ReturnUrl). Yalnız bilgilendirme — gerçek sipariş
// durumu webhook'tan (PaymentSucceeded/Failed) belirlenir, bu ekrandan DEĞİL. Akış chat-only. Anonim.
public static class CheckoutDoneEndpointExtension
{
    public static void AddCheckoutDoneEndpoint(this WebApplication app)
    {
        app.MapGet("/checkout/done", () => Results.Content(
                "<html><body><h3>Ödeme işleminiz alındı.</h3><p>Sohbetinize dönebilirsiniz.</p></body></html>",
                "text/html"))
            .WithTags("checkout")
            .AllowAnonymous();
    }
}
