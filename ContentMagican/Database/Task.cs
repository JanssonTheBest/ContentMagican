namespace ContentMagican.Database
{
    public class _Task
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Type { get; set; }
        public int Subtype { get; set; }
        public string Description { get; set; }
        public int Status { get; set; }
        public DateTime Created { get; set; }
        public string AdditionalInfo { get; set; }
    }
}
