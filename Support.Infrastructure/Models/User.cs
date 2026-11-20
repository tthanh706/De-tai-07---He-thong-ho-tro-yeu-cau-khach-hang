
namespace Support.Infrastructure.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Customer";

    public ICollection<Ticket> Tickets { get; set; }
        = new List<Ticket>();
}
