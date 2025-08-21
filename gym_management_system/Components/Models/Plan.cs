namespace gym_management_system.Components.Models
{
    //this class is been made 
    //to show the plans available
    //for the clients in the gym
    public class Plan : GymEntity, GymPlan
    {
        public string Name { get; set; }
        public string Duration { get; set; }
        public string Price { get; set; }

        public override void DisplayDetails()
        {
            Console.WriteLine($"Plan: {Name}, Duration: {Duration}, Price: {Price}");
        }
    }
}
