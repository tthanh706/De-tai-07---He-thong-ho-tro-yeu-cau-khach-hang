
namespace Support.Infrastructure.Models;

public class Ticket
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "New";

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public int? AssignedTo { get; set; }
    public User? Assignee { get; set; }

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; }
        = DateTime.UtcNow;

    public ICollection<TicketHistory> Histories { get; set; }
        = new List<TicketHistory>();
}
