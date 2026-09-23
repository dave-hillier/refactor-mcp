namespace Staff
{
    public class Employee : Person
    {
        #region Delegate
        // The person this employee is.
        private readonly Person _person = new Person();
        #endregion

        public string Badge() => _person.Greeting("Employee");
    }
}
