namespace Staff
{
    public class Employee
    {
        protected string _name;

        /// <summary>The name as printed on a badge.</summary>
        public string Badge()
        {
            return "Name: " + _name;
        }
    }
}
