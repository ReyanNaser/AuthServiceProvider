using AuthServiceProvider;
using AuthServiceProvider.Data;
using AuthServiceProvider.Features;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<Microsoft.AspNetCore.Identity.IdentityUser, Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.AddHostedService<SeedWorker>();

builder.Services.AddOpenIddict()
    .AddCore(options => {
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options => {
        // Enable endpoints
        options.SetTokenEndpointUris("/connect/token");

        // Use Client Credentials or Password flow
        options.AllowPasswordFlow();
        options.AllowRefreshTokenFlow();

        // Encryption/signing credentials (use a real certificate in production)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough();

        options.DisableAccessTokenEncryption();
    })
    .AddValidation(options => {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var app = builder.Build();

app.MapRegisterEndpoint();
app.MapLoginEndpoint();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGrpcService<AuthServiceProvider.Services.GrpcAuthService>();
app.MapGrpcService<AuthServiceProvider.Services.GrpcUpdateRoleService>();

app.Run();
