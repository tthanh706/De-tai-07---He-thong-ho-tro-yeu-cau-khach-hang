
using Microsoft.EntityFrameworkCore;
using Support.Contracts.Tickets;
using Support.Infrastructure.Data;
using Support.Infrastructure.Models;

var builder = WebApplication.CreateBuilder(args);

// Kết nối PostgreSQL
builder.Services.AddDbContext<SupportDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

// Kiểm tra API
app.MapGet("/", () =>
    "API hỗ trợ khách hàng đang hoạt động!");

// Kiểm tra kết nối database
app.MapGet("/health/db", async (SupportDbContext db) =>
{
    bool connected = await db.Database.CanConnectAsync();

    return connected
        ? Results.Ok(new { database = "Connected" })
        : Results.StatusCode(503);
});

// Tạo yêu cầu hỗ trợ mới
app.MapPost("/tickets", async (
    CreateTicketRequest request,
    SupportDbContext db,
    ILogger<Program> logger) =>
{
    // Kiểm tra dữ liệu đầu vào
    if (request.UserId <= 0 ||
        string.IsNullOrWhiteSpace(request.Title) ||
        string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest(
            new { message = "Dữ liệu yêu cầu không hợp lệ." });
    }

    if (request.Title.Trim().Length > 200)
    {
        return Results.BadRequest(
            new { message = "Tiêu đề không được quá 200 ký tự." });
    }

    string priority = request.Priority?.Trim() ?? "";

    if (!new[] { "Low", "Medium", "High" }
        .Contains(priority, StringComparer.OrdinalIgnoreCase))
    {
        return Results.BadRequest(
            new { message = "Mức ưu tiên phải là Low, Medium hoặc High." });
    }

    // Kiểm tra khách hàng tồn tại
    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Id == request.UserId);

    if (user == null)
    {
        return Results.NotFound(
            new { message = "Không tìm thấy người dùng." });
    }

    if (user.Role != "Customer")
    {
        return Results.BadRequest(
            new { message = "Chỉ khách hàng mới được tạo yêu cầu." });
    }

    // Khởi tạo Ticket
    var ticket = new Ticket
    {
        UserId = request.UserId,
        Title = request.Title.Trim(),
        Content = request.Content.Trim(),
        Priority = priority.ToUpperInvariant() switch
        {
            "LOW" => "Low",
            "HIGH" => "High",
            _ => "Medium"
        },
        Status = "New",
        TeamId = null,
        AssignedTo = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // Lưu Ticket và lịch sử đầu tiên
    db.Tickets.Add(ticket);

    db.TicketHistories.Add(new TicketHistory
    {
        Ticket = ticket,
        OldStatus = null,
        NewStatus = "New",
        ChangedBy = request.UserId,
        ChangedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    logger.LogInformation(
        "Đã tạo Ticket {TicketId} cho khách hàng {UserId}",
        ticket.Id,
        ticket.UserId);

    return Results.Created($"/tickets/{ticket.Id}", new
    {
        ticket.Id,
        ticket.UserId,
        ticket.Title,
        ticket.Content,
        ticket.Priority,
        ticket.Status,
        ticket.TeamId,
        ticket.AssignedTo,
        ticket.CreatedAt
    });
});


 // Lấy danh sách tất cả yêu cầu hỗ trợ
app.MapGet("/tickets", async (SupportDbContext db) =>
{
    var tickets = await db.Tickets
        .AsNoTracking()
        .Include(t => t.User)
        .Include(t => t.Team)
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new
        {
            t.Id,
            t.Title,
            t.Content,
            t.Priority,
            t.Status,
            t.CreatedAt,
            CustomerName = t.User.FullName,
            TeamName = t.Team != null
                ? t.Team.Name
                : "Chưa phân công"
        })
        .ToListAsync();

    return Results.Ok(tickets);
});


 // API xem chi tiết yêu cầu hỗ trợ
app.MapGet("/tickets/{id:int}", async (
    int id,
    SupportDbContext db) =>
{
    var ticket = await db.Tickets
        .AsNoTracking()
        .Where(t => t.Id == id)
        .Select(t => new
        {
            t.Id,
            t.UserId,
            CustomerName = t.User.FullName,
            t.Title,
            t.Content,
            t.Priority,
            t.Status,
            t.TeamId,
            TeamName = t.Team != null
                ? t.Team.Name
                : "Chưa phân công",
            t.AssignedTo,
            AssigneeName = t.Assignee != null
                ? t.Assignee.FullName
                : "Chưa có nhân viên",
            t.CreatedAt,
            t.UpdatedAt,
            History = t.Histories
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new
                {
                    h.Id,
                    h.OldStatus,
                    h.NewStatus,
                    h.OldAssigneeId,
                    h.NewAssigneeId,
                    h.ChangedBy,
                    h.ChangedAt
                })
                .ToList()
        })
        .FirstOrDefaultAsync();

    if (ticket == null)
    {
        return Results.NotFound(
            new { message = "Không tìm thấy yêu cầu hỗ trợ." });
    }

    return Results.Ok(ticket);
});


app.MapPatch("/tickets/{id:int}/status", async (
    int id,
    UpdateTicketStatusRequest request,
    SupportDbContext db,
    ILogger<Program> logger) =>
{
    // Kiểm tra Ticket tồn tại
    var ticket = await db.Tickets.FindAsync(id);

    if (ticket == null)
    {
        return Results.NotFound(
            new { message = "Không tìm thấy yêu cầu." });
    }

    // Kiểm tra nhân viên
    var staff = await db.Users.FindAsync(request.StaffId);

    if (staff == null ||
        (staff.Role != "Staff" && staff.Role != "Admin"))
    {
        return Results.BadRequest(
            new { message = "Nhân viên không hợp lệ." });
    }

    // Kiểm tra trạng thái
    string newStatus = request.Status?.Trim() ?? "";

    string[] validStatuses =
    {
        "New",
        "Assigned",
        "InProgress",
        "Resolved"
    };

    if (!validStatuses.Contains(newStatus))
    {
        return Results.BadRequest(
            new { message = "Trạng thái không hợp lệ." });
    }

    // Không cập nhật nếu trạng thái không thay đổi
    if (ticket.Status == newStatus)
    {
        return Results.BadRequest(
            new { message = "Ticket đã ở trạng thái này." });
    }

    // Lưu trạng thái cũ
    string oldStatus = ticket.Status;

    // Cập nhật trạng thái mới
    ticket.Status = newStatus;
    ticket.UpdatedAt = DateTime.UtcNow;

    // Ghi lịch sử thay đổi
    db.TicketHistories.Add(new TicketHistory
    {
        TicketId = ticket.Id,
        OldStatus = oldStatus,
        NewStatus = newStatus,
        ChangedBy = request.StaffId,
        ChangedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    logger.LogInformation(
        "Ticket {TicketId} đổi trạng thái từ {OldStatus} sang {NewStatus}",
        ticket.Id,
        oldStatus,
        newStatus);

    return Results.Ok(new
    {
        ticket.Id,
        OldStatus = oldStatus,
        NewStatus = ticket.Status,
        ticket.UpdatedAt
    });
});


 // API lấy danh sách Ticket theo người dùng
app.MapGet("/users/{userId:int}/tickets", async (
    int userId,
    SupportDbContext db) =>
{
    // Kiểm tra người dùng có tồn tại không
    var user = await db.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (user == null)
    {
        return Results.NotFound(new
        {
            message = "Không tìm thấy người dùng."
        });
    }

    // Lấy tất cả Ticket của người dùng
    var tickets = await db.Tickets
        .AsNoTracking()
        .Where(t => t.UserId == userId)
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new
        {
            t.Id,
            t.Title,
            t.Content,
            t.Priority,
            t.Status,
            t.CreatedAt,
            TeamName = t.Team != null
                ? t.Team.Name
                : "Chưa phân công"
        })
        .ToListAsync();

    return Results.Ok(new
    {
        UserId = user.Id,
        CustomerName = user.FullName,
        TotalTickets = tickets.Count,
        Tickets = tickets
    });
});

app.Run();
