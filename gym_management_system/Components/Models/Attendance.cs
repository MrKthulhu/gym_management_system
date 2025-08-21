namespace gym_management_system.Components.Models
{
    //this interface has been created to 
    //keep the attendance record of the members
    //of the gym and taking the attendace 
    //according to the exact date and time
    public interface IAttendance
    {
        string MemberName { get; set; }
        DateTime Date { get; set; }
    }
}
