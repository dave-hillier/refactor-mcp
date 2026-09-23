namespace Library;

public class Member
{
    public const string Student = "S";
    public const string Staff = "T";

    private string? _status;

    public void Enrol(string status)
    {
        _status = status;
    }

    public bool IsStudent => _status == Student;

    public bool IsEnrolled => _status != null;
}
