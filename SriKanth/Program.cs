using HRIS.API.Infrastructure.Helpers;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SriKanth.Data;
using SriKanth.Interface;
using SriKanth.Interface.Data;
using SriKanth.Interface.Login_Module;
using SriKanth.Interface.Notification;
using SriKanth.Model;
using SriKanth.Service;
using SriKanth.Service.Login_Module;
using SriKanth.Service.Notification;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailNotification, EmailNotification>();
builder.Services.AddScoped<ISmsNotification, SmsNotification>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<TrackLogin>();
builder.Services.AddTransient<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<ILoginData, LoginData>();
builder.Services.AddScoped<IExternalApiService, ExternalApiService>();
builder.Services.AddScoped<IBusinessApiService, BusinessApiService>();
builder.Services.AddScoped<IBusinessData, BusinessData>();
builder.Services.AddScoped<IUserHistoryService, UserHistoryService>();
builder.Services.AddScoped<UserHistoryActionFilter>();



builder.Services.AddDbContext<SriKanthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("SriKanth.Model")));

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidIssuer = builder.Configuration["Jwt:Issuer"],
		ValidateAudience = true,
		ValidAudience = builder.Configuration["Jwt:Audience"],
		ValidateLifetime = true,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
		NameClaimType = JwtRegisteredClaimNames.Name
	};

	// JWT validation event
	options.Events = new JwtBearerEvents
	{
		OnTokenValidated = context =>
		{
			var token = context.SecurityToken as JwtSecurityToken;
			if (token != null)
			{
				var claimsIdentity = context.Principal.Identity as ClaimsIdentity;

				// Validate required claims (email, name, and phone number)
					var emailClaim = claimsIdentity?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
					var nameClaim = claimsIdentity?.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
					var phoneNumberClaim = claimsIdentity?.FindFirst(ClaimTypes.MobilePhone)?.Value;

                if (string.IsNullOrEmpty(emailClaim) || string.IsNullOrEmpty(nameClaim) || string.IsNullOrEmpty(phoneNumberClaim))
                {
                    context.Fail("Unauthorized: Missing required claims.");
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }
    };
});

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Information()
	.WriteTo.Console() // Optional: Log to console too
	.WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
	.CreateLogger();

builder.Services.AddCors(o =>
{
	o.AddPolicy("corspolicy", build =>
	{
		build.WithOrigins("http://localhost:3000")
			 .AllowAnyMethod()
			 .AllowAnyHeader()
			 .AllowCredentials();
	});
});


builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter(); // Enable rate limiting middleware

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
