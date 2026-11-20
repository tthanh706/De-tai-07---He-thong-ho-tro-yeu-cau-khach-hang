
namespace Support.Infrastructure.Models;

public class TicketHistory
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    public int? OldAssigneeId { get; set; }
    public int? NewAssigneeId { get; set; }

    public int? ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; }
        = DateTime.UtcNow;
}
