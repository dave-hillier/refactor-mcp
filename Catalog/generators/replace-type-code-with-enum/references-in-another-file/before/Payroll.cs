using Staff;

namespace Accounts;

public class Payroll
{
    public Employee Hire(string name) => new Employee(Employee.Manager, name);

    public string Describe(Employee employee)
    {
        int code = employee.Type;
        if (code == Employee.Engineer)
            return "engineer";
        return Label(employee.Type);
    }

    private static string Label(int code) => code == Employee.Manager ? "manager" : "staff";

    private static string Label(string text) => text;
}
