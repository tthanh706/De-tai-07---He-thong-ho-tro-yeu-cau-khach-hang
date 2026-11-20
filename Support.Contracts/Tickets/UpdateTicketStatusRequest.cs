
namespace Support.Contracts.Tickets;

public class UpdateTicketStatusRequest
{
    public string Status { get; set; } = "";

    public int StaffId { get; set; }
}
