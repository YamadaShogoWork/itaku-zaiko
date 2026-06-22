using Microsoft.AspNetCore.Identity;

namespace Zaiko.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; }
}
