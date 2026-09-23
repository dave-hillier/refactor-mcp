namespace Library;

public class Member
{
    private MemberStatus? _status;

    public void Enrol(MemberStatus status)
    {
        _status = status;
    }

    public bool IsStudent => _status == MemberStatus.Student;

    public bool IsEnrolled => _status != null;
}
