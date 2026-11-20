
namespace Support.Contracts.Tickets;

public class CreateTicketRequest
{
    public int UserId { get; set; }

    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    public string Priority { get; set; } = "Medium";
}
