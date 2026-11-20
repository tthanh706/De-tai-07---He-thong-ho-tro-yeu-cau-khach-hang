
namespace Support.Infrastructure.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public ICollection<Ticket> Tickets { get; set; }
        = new List<Ticket>();
}
