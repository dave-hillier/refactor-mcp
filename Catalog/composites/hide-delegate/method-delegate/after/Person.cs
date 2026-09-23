namespace Staff
{
    public class Person
    {
        public string Name { get; set; } = "";

        public Department Department { get; set; } = new Department();

        public string DescribeDepartment(string prefix) => Department.Describe(prefix);
    }
}
