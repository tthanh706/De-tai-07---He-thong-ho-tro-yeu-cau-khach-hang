
using Microsoft.EntityFrameworkCore;
using Support.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SupportDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

app.MapGet("/", () =>
    "API hỗ trợ khách hàng đang hoạt động!");

app.MapGet("/health/db", async (SupportDbContext db) =>
{
    bool connected = await db.Database.CanConnectAsync();

    return connected
        ? Results.Ok(new { database = "Connected" })
        : Results.StatusCode(503);
});

app.Run();
