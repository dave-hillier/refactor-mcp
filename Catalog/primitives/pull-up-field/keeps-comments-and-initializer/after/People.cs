namespace Shop
{
    public class Employee
    {
        // Pay is monthly.
        protected decimal Salary;
        /// <summary>Days of leave a year.</summary>
        protected int Holidays = 25;
    }

    public class Manager : Employee
    {
        // Managers are senior.
        public int Grade;

        // Approvals follow.
        public bool CanApprove() => Grade > 2 && Holidays > 0;
    }
}
