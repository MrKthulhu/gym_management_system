namespace gym_management_system.Components.Models
{
    //this class has been made
    //to keep the credentials of the clients 
    //to get registered in the gym
    public class Member
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }
        public string Plan { get; set; }
        public int Id { get; internal set; }
    }
}
