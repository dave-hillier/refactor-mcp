using Staff;

namespace Recruiting;

public class Hiring
{
    public Employee HireSalesman(string name) => new Salesman(name);

    public Employee Hire(EmployeeType type, string name)
    {
        var hired = Employee.Create(type, name);
        return hired;
    }
}
