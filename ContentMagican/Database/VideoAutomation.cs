namespace ContentMagican.Database
{
    public class VideoAutomation
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string FFmpegString { get; set; }
        public int Interval { get; set; }

    }
}
