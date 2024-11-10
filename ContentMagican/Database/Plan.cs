namespace ContentMagican.Database
{
    public class Plan
    {
        public int PlanId { get; set; } 
        public string PlanName { get; set; }
        public decimal Price { get; set; }
        public int DurationInMonths { get; set; }
        public string? Description { get; set; }
        public bool RequireAccountVerification { get; set; }

    }
}
