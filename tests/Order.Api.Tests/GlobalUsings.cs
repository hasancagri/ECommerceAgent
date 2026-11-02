global using Xunit;
global using Shouldly;
global using Order.Api.Domains.Orders;
global using Order.Api.Domains.Orders.ValueObjects;
// Kok namespace 'Order' ile aggregate tipi 'Order' cakisiyor; tipe alias veriyoruz.
global using OrderAggregate = Order.Api.Domains.Orders.Order;
// --- hoisted (sade using'ler dosyalardan taşındı) ---
