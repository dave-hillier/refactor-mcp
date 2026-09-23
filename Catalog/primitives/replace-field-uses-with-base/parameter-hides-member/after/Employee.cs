namespace Staff
{
    public class Employee : Person
    {
        public string Retitle(string title)
        {
            var old = this.title;
            this.title = title;
            return old;
        }
    }
}
