// <copyright file="Program.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.DataProtection;
using TAGWEBAPI.Data;
using TAGWEBAPI.Integrations.ModernTreasury;
using TAGWEBAPI.Models;
using TAGWEBAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi.Models;
using TAGWEBAPI.Services;

Console.WriteLine("========================================");
Console.WriteLine("Setting Up TAG WEB API...");
Console.WriteLine("========================================");

var textprefix = "==--->>>|";

var builder = WebApplication.CreateBuilder(args);

// Retrieve the Application Insights connection string from the environment variable
// Skip entirely in Development to keep the debug console readable
var appInsightsConnectionString = builder.Configuration.GetConnectionString("appinsights");
if (!builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(appInsightsConnectionString))
{
    // Register Application Insights carefully - failures during design-time (dotnet-ef)
    // or mismatched package/tooling versions can throw when listeners are attached.
    try
    {
        builder.Services.AddApplicationInsightsTelemetry(appInsightsConnectionString);
        Console.WriteLine(textprefix + "Application Insights telemetry enabled.");
    }
    catch (Exception aiEx)
    {
        // Don't allow telemetry registration to abort the host build, especially during design-time operations.
        Console.WriteLine(textprefix + "Could not enable Application Insights telemetry. Continuing without it.");
        Console.WriteLine("Telemetry registration error: " + aiEx.Message);
    }
}
else if (builder.Environment.IsDevelopment())
{
    Console.WriteLine(textprefix + "Application Insights skipped in Development environment.");
}
else
{
    Console.WriteLine("Application Insights connection string not found. Telemetry is disabled.");
}

// Validate the database connection string
var dbConnectionString = builder.Configuration.GetConnectionString("tag_web_db");
if (string.IsNullOrEmpty(dbConnectionString))
{
    Console.WriteLine("========================================");
    Console.WriteLine("ERROR: Database connection string 'tag_web_db' is not initialized.");
    Console.WriteLine("========================================");
    throw new InvalidOperationException("Database connection string 'tag_web_db' is not configured.");
}
else
{
    Console.WriteLine(textprefix + "Database connection string 'tag_web_db' is initialized.");
}

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // THIS PREVENTS OBJECT CYCLE CRASHES GLOBALLY
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
Console.WriteLine(textprefix + "Controllers added to the service container (CamelCase naming policy configured).");

builder.Services.AddDbContext<TAGDBContext>(options =>
    options.UseNpgsql(dbConnectionString));
Console.WriteLine(textprefix + "Database context configured with PostgreSQL.");

Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

// ==========================================
// 1. ADD JWT AUTHENTICATION FOR NEXTAUTH 
// ==========================================
var nextAuthSecret = builder.Configuration["NextAuthSecret"]; // (reference env.local) -> dotnet user-secrets set NextAuthSecret XXXX 

if (string.IsNullOrEmpty(nextAuthSecret))
{
    Console.WriteLine("========================================");
    Console.WriteLine("WARNING: Configuration value 'NextAuthSecret' is not set. JWT authentication will not be configured; [Authorize] endpoints will fail.");
    Console.WriteLine("========================================");
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var keyBytes = Encoding.UTF8.GetBytes(nextAuthSecret);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        signingKey.KeyId = "nextauth-key";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            IssuerSigningKeyResolver = (tokenString, securityToken, kid, parameters) =>
            {
                return new[] { signingKey };
            },
            
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireSignedTokens = true,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // First check native Header authorization (for Swagger testing)
                var headerAuth = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(headerAuth) && headerAuth.StartsWith("Bearer "))
                {
                    context.Token = headerAuth.Substring("Bearer ".Length).Trim();
                    return Task.CompletedTask;
                }

                // If no header, grab from proxy cookies (For live React application)
                var token = context.Request.Cookies["next-auth.session-token"] 
                         ?? context.Request.Cookies["__Secure-next-auth.session-token"];
                         
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    });
}
// ==========================================

builder.Services.Configure<ModernTreasuryOptions>(
    builder.Configuration.GetSection(ModernTreasuryOptions.SectionName));

builder.Services.AddHttpClient<IModernTreasuryLedgerService, ModernTreasuryLedgerService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
Console.WriteLine(textprefix + "Modern Treasury ledger service configured.");

builder.Services.AddSingleton<PricingCalculatorService>();
Console.WriteLine(textprefix + "Pricing calculator service configured.");


// Ensure Data Protection services are registered for server-side envelope protection (messages)
builder.Services.AddDataProtection();
Console.WriteLine(textprefix + "Data Protection services registered.");

// Register SignalR
builder.Services.AddSignalR();
Console.WriteLine(textprefix + "SignalR services registered.");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// Configure Swagger to accept Bearer tokens
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your NextAuth token here to test secured APIs. Do NOT type 'Bearer ' before the token, just paste the raw token string."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Define a named CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", builder =>
    {
        builder
            .WithOrigins(
                "https://dev.twistedartistsguild.com",
                "https://twistedartistsguild.com",
                "https://www.twistedartistsguild.com", // Note for the future that CORS can keep dev, staging, and prod responding to only their peer group
                "https://staging.twistedartistsguild.com",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Allow credentials for cross-origin requests
    });
});
Console.WriteLine(textprefix + "CORS policy 'AllowSpecificOrigins' defined.");

try
{
    var app = builder.Build();
#pragma warning disable CA1303 // Do not pass literals as localized parameters
    Console.WriteLine(textprefix + "Application built successfully.");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

    // Apply the CORS policy globally
    app.UseCors("AllowSpecificOrigins");
    Console.WriteLine(textprefix + "CORS policy applied globally.");

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        Console.WriteLine(textprefix + "Swagger enabled for API documentation.");
    }

    if (app.Environment.IsProduction()) // Temporary for testing, remove in future
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        Console.WriteLine(textprefix + "Swagger enabled for API documentation.");
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    if (app.Environment.IsProduction() || app.Environment.IsStaging())
    {
        app.UseHttpsRedirection();
    }

    // Serve static files (e.g. uploaded attachments under wwwroot/uploads/messages)
    app.UseStaticFiles();
    Console.WriteLine(textprefix + "Static file middleware enabled (wwwroot).");

    // MAKE SURE USE_AUTHENTICATION IS ADDED BEFORE USE_AUTHORIZATION
    app.UseAuthentication(); // <-- ADD THIS LINE
    app.UseAuthorization();
    
    app.MapControllers();
    // Map messaging hub
    app.MapHub<MessagingHub>("/hubs/messaging");

    Console.WriteLine("========================================");
    Console.WriteLine("TAG WEB API is starting: app.run()");
    Console.WriteLine("========================================");
    app.Run();
}
catch (Exception ex)
{
    // Always log to console first for immediate visibility
    Console.WriteLine("========================================");
    Console.WriteLine("Exception caught in Program.cs:");
    // Print the full exception (includes inner exceptions) to help debugging during design-time operations
    Console.WriteLine(ex.ToString());
    Console.WriteLine("========================================");

    // Attempt to log to database - with smarter exception handling
    // If the host was aborted by a diagnostic listener (HostAbortedException) it's often
    // informational during design-time runs; skip trying to resolve services in that case
    if (ex is HostAbortedException)
    {
        Console.WriteLine("Host aborted during build; skipping database logging to avoid cascading failures.");
    }
    else
    {
        try
        {
            using (var scope = builder.Services.BuildServiceProvider().CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TAGDBContext>();

                // Simple database connection check
                if (db.Database.CanConnect())
                {
                    var log = new Log
                    {
                        Tags = "Program.cs",
                        ShortText = "ERROR",
                        Critical = true,
                        LoggedData = $"Message: {ex.Message} \r\n Stack: {ex.StackTrace}",
                        LogTimestamp = DateTime.UtcNow, // Always use UTC time
                    };
                    db.Logs.Add(log);
                    db.SaveChanges();
                    Console.WriteLine("Error successfully logged to database.");
                }
                else
                {
                    Console.WriteLine("Database connection failed - error logged to console only.");
                }
            }
        }
        catch (Exception dbEx)
        {
            // If database logging fails, just note it and continue
            // This handles DB doesn't exist, permissions issues, etc. 
            Console.WriteLine("Could not log to database:");
            Console.WriteLine($"Reason: {dbEx.Message}");

            // No need to re-log the original exception - it's already in the console output
        }
    }
}
