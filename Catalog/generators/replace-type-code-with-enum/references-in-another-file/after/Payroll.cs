using Staff;

namespace Accounts;

public class Payroll
{
    public Employee Hire(string name) => new Employee(EmployeeType.Manager, name);

    public string Describe(Employee employee)
    {
        EmployeeType code = employee.Type;
        if (code == EmployeeType.Engineer)
            return "engineer";
        return Label(employee.Type);
    }

    private static string Label(EmployeeType code) => code == EmployeeType.Manager ? "manager" : "staff";

    private static string Label(string text) => text;
}
