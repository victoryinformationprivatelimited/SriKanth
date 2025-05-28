using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SriKanth.Interface.Login_Module;
using SriKanth.Interface.Notification;
using SriKanth.Interface;
using SriKanth.Model;
using SriKanth.Service.Login_Module;
using SriKanth.Service.Notification;
using SriKanth.Service;
using SriKanth.Interface.Data;
using SriKanth.Data;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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

// In Program.cs (or Startup.ConfigureServices for older versions)
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
				PermitLimit = 5, // Allow only 5 requests
				Window = TimeSpan.FromMinutes(1), // Per 1 minute
				QueueLimit = 0 // No queueing
			}));
});
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Required for IHttpContextAccessor
builder.Services.AddHttpContextAccessor();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization(); 
app.MapControllers();

app.Run();
