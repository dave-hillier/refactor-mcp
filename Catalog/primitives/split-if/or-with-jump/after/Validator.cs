namespace Shop
{
    public class Validator
    {
        public string Check(string name, int age)
        {
            // Reject what cannot be stored.
            if (name == null)
            {
                return "invalid";
            }

            if (age < 0)
            {
                return "invalid";
            }

            return "valid";
        }
    }
}
