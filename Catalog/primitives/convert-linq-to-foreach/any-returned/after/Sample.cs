using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public bool HasAdmin(List<User> users)
    {
        // Admins can approve.
        foreach (var user in users)
        {
            if (user.IsAdmin)
            {
                return true;
            }
        }

        return false;
    }
}
