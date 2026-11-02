global using Asp.Versioning;
global using Common.Domains;
global using Common.Results;
global using Common.Exceptions;
global using Common.Extensions;
global using Common;
global using Marten;
global using Marten.Newtonsoft;
global using Weasel.Core;
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

// --- hoisted (sade using'ler dosyalardan taşındı) ---
global using Common.Dependencies;
global using Shared.Utils.Constants;

// auth + options (093: PG istemci/callback/gRPC using'leri sökümle düştü).
global using Platform.Auth;
global using Microsoft.Extensions.Options;
global using Shared;
global using Payment.Api.Extensions;
