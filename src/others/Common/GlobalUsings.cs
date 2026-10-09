global using System.Reflection;
global using System.Security.Claims;
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Caching.Hybrid;
global using Microsoft.Extensions.DependencyInjection;
global using Wolverine;
global using Common.Results.BaseClasses;

// --- hoisted (2+ dosyada tekrar; using consolidation) ---
global using Platform.Auth;
global using Common.Utils.Constants;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Logging;
global using PagedList.Core;
global using StackExchange.Redis;
