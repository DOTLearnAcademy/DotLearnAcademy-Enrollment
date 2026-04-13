using Amazon.SQS;
using DotLearn.Enrollment.Data;
using DotLearn.Enrollment.Middleware;
using DotLearn.Enrollment.Repositories;
using DotLearn.Enrollment.Services;
using DotLearn.Enrollment.Workers;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Amazon;

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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication & Authorization
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "dotlearn-auth",
            ValidateAudience = false, // Not using audience currently
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "placeholder-key-32-chars-minimum!"))
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
