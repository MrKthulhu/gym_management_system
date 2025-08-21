namespace gym_management_system.Components.Models
{
    //this interface has been created for the
    //gym plan conscience including the name
    //duration and price accordingly
    public interface GymPlan
    {
        string Name { get; set; }
        string Duration { get; set; }
        string Price { get; set; }
    }
}