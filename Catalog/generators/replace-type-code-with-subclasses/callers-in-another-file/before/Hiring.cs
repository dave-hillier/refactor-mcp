using Staff;

namespace Recruiting;

public class Hiring
{
    public Employee HireSalesman(string name) => new Employee(EmployeeType.Salesman, name);

    public Employee Hire(EmployeeType type, string name)
    {
        var hired = new Employee(type, name);
        return hired;
    }
}
