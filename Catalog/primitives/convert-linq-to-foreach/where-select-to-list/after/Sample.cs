using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> ActiveNames(List<User> users)
    {
        var names = new List<string>();
        foreach (var user in users)
        {
            if (user.IsActive)
            {
                names.Add(user.Name);
            }
        }

        return names;
    }
}
