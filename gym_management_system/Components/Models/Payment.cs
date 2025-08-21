namespace gym_management_system.Components.Models
{
    //this class has been made to
    //keep the payment process simple
    //and suitable for the clients
    public class Payment
    {
        public string Member { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}

