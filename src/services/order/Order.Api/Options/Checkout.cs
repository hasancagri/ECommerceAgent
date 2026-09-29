namespace Order.Api.Options;

// checkout watchdog config — section "Checkout". CheckoutSaga.Start buradan tip'li okur.
public class Checkout
{
    public int WatchdogSeconds { get; set; } = 120;
}
