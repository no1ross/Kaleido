namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts;

public enum MemberGender
{
    Unknown = 0,
    Female = 1,
    Male = 2,
    NonBinary = 3
}

public enum EnrollmentStatus
{
    Unknown = 0,
    Active = 1,
    Pending = 2,
    Termed = 3,
    Cobra = 4
}

public enum LineOfBusiness
{
    Unknown = 0,
    Commercial = 1,
    Medicare = 2,
    Medicaid = 3,
    Exchange = 4
}

public enum RelationshipToSubscriber
{
    Unknown = 0,
    Subscriber = 1,
    Spouse = 2,
    Child = 3,
    OtherDependent = 4
}
