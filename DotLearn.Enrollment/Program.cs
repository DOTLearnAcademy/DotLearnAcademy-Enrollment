using Amazon.SQS;
using DotLearn.Enrollment.Data;
using DotLearn.Enrollment.Middleware;
using DotLearn.Enrollment.Repositories;
using DotLearn.Enrollment.Services;
using DotLearn.Enrollment.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Amazon;
using System.Security.Claims;
using System.Net.Http;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// AWS Secrets Manager (Only in non-Development environments)
if (!builder.Environment.IsDevelopment())
{
    // builder.Configuration.AddSecretsManager(region: RegionEndpoint.APSoutheast2);
}

// Add services to the container.
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EnrollmentDbContext>(options =>
    options.UseSqlServer(connStr));

builder.Services.AddHttpClient();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddHostedService<PaymentSucceededConsumer>();
builder.Services.AddDefaultAWSOptions(new Amazon.Extensions.NETCore.Setup.AWSOptions
{
    Region = Amazon.RegionEndpoint.APSoutheast2
});
builder.Services.AddAWSService<IAmazonSQS>();

builder.Services.AddHealthChecks().AddSqlServer(connStr);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<DotLearn.Enrollment.Middleware.CorrelationIdDelegatingHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication & Authorization — manual JWKS loading (no OIDC discovery needed)
var jwksUri = builder.Configuration["Auth:JwksUri"]
    ?? "http://auth/auth/.well-known/jwks.json";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "dotlearn-auth",
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                using var http = new HttpClient();
                var json = http.GetStringAsync(jwksUri).GetAwaiter().GetResult();
                var jwks = new JsonWebKeySet(json);
                return jwks.GetSigningKeys();
            },
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// CORS — DOT-24 Security Lockdown
builder.Services.AddCors(options =>
{
    options.AddPolicy("DotLearnPolicy", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                builder.Configuration["AllowedOrigins:Ec2"] ?? "",
                builder.Configuration["AllowedOrigins:CloudFront"] ?? "")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Middlewares
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandler>();

app.UseCors("DotLearnPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
