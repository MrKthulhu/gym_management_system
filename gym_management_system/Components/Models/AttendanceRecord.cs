namespace gym_management_system.Components.Models
{
    //this class has been made to track 
    //the attendance record of members and trainers
    //and also to avoid manual attendance
    public class AttendanceRecord : GymEntity, IAttendance
    {
        public string MemberName { get; set; }
        public DateTime Date { get; set; }

        public override void DisplayDetails()
        {
            Console.WriteLine($"Member: {MemberName}, Date: {Date.ToShortDateString()}");
        }
    }
}


