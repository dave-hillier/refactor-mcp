namespace Security
{
    public class Access
    {
        public bool Allowed(bool admin, bool owner, bool locked, bool strict)
        {
            if (locked && !admin || (strict ? !owner : !owner && !admin))
                return false;
            return true;
        }
    }
}
