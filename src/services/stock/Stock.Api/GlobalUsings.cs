global using Asp.Versioning.Builder;
global using Asp.Versioning;
global using Common.Domains;
global using Common.Exceptions;
global using Common.Extensions;
global using Common.Utils.Constants;
global using Common;
global using Common.Results;
global using Marten.Newtonsoft;
global using Marten;
global using Microsoft.AspNetCore.Mvc;
global using Newtonsoft.Json;
global using Shared;
global using Stock.Api.Constants;
global using Stock.Api.Dependencies;
global using Stock.Api.Domains.Stocks.Features.Agents.Commands;
global using Stock.Api.Domains.Stocks.Features.Agents.Queries;
global using Stock.Api.Domains.Stocks;
global using Stock.Api.Domains.Stocks.Entities;
global using System.Reflection;
global using System;
global using Weasel.Core;
global using Wolverine.Attributes;
global using Wolverine.Marten;
global using Wolverine.RabbitMQ;
global using Wolverine;
global using Wolverine.ErrorHandling;
global using System.ComponentModel;
global using ModelContextProtocol.Server;
global using Shared.Utils.Constants;
// --- hoisted (sade using'ler dosyalardan taşındı) ---
global using Common.Dependencies;
// --- 070: admin MCP yüzeyi ---
global using Common.Utils.Authorization;
global using Common.Auths;
global using Stock.Api.Extensions;

global using Platform.Auth;
