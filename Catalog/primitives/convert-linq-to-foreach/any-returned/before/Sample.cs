using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public bool HasAdmin(List<User> users)
    {
        // Admins can approve.
        return /*^*/users.Any(user => user.IsAdmin);
    }
}
