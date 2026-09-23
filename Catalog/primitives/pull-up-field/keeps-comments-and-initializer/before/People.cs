namespace Shop
{
    public class Employee
    {
        // Pay is monthly.
        protected decimal Salary;
    }

    public class Manager : Employee
    {
        // Managers are senior.
        public int Grade;

        /// <summary>Days of leave a year.</summary>
        protected int Holidays = 25;

        // Approvals follow.
        public bool CanApprove() => Grade > 2 && Holidays > 0;
    }
}
