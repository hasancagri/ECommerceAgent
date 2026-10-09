global using Asp.Versioning.Builder;
global using Asp.Versioning;
global using Common.Domains;
global using Common.Results;
global using Common.Exceptions;
global using Common.Extensions;
global using Common;
global using Marten;
global using Marten.Newtonsoft;
global using Weasel.Core;
global using Microsoft.AspNetCore.Mvc;
global using Payment.Api.Dependencies;
global using Payment.Api.Domains.Payments;
global using System.Reflection;
global using System;
global using Wolverine.Attributes;
global using Wolverine.Marten;
global using Wolverine.RabbitMQ;
global using Wolverine;
global using Common.Utils.Constants;
global using Payment.Api.Constants;
global using Common.Auths;
global using System.ComponentModel;
global using ModelContextProtocol.Server;

// --- hoisted (sade using'ler dosyalardan taşındı) ---
global using Common.Dependencies;
global using Payment.Api.Domains.Payments.Features.Agents;
global using Shared.Utils.Constants;

// hosted-CF ödeme — options + S2S + auth.
global using Platform.Auth;
global using Common.Utils.Authorization;
global using Payment.Api.Options;
global using Microsoft.Extensions.Options;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.ComponentModel.DataAnnotations;
global using System.Security.Cryptography;
global using System.Text;
global using System.Text.Json;
global using System.Net.Http;
global using Payment.Api.Infrastructure;
global using Shared;

// (gRPC'ye taşındı): PaymentIntent S2S sunucu + MerchantKey S2S istemci.
global using Grpc.Core;
global using Shared.Grpc.Payment;
global using Shared.Grpc.Customer;
global using Payment.Api.Grpc;
global using Payment.Api.Grpc.HostedPayment;
global using Payment.Api.Extensions;
