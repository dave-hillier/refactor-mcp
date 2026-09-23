namespace Staff
{
    public class Reception
    {
        public string Greet(Person person) => "Please ask " + person.Department.Manager.Name;

        public void Reorganise(Person person, Employee manager)
        {
            // Assigning through the department stays as it is.
            person.Department.Manager = manager;
        }
    }
}
