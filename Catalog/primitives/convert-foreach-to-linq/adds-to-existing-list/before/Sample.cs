using System.Collections.Generic;

public class Sample
{
    private readonly List<string> _names = new List<string>();

    public void AddActive(List<User> users)
    {
        /*^*/foreach (var user in users)
        {
            if (user.IsActive)
            {
                _names.Add(user.Name);
            }
        }
    }
}
