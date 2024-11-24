namespace ContentMagican.Models
{
    public class EditTaskViewModel
    {
        public string VideoDimensions { get; set; } = string.Empty;

        public bool VerticalResolution { get; set; } = false;

        public string TextStyle { get; set; } = string.Empty;

        public string GameplayVideo { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public int VideoLengthFrom { get; set; } = 0;

        public int VideoLengthTo { get; set; } = 0;

        public int VideosPerDay { get; set; } = 1;

        public string AdditionalInfo { get; set; } = string.Empty;
        public string VideoTitle { get; set; } = string.Empty;

        public long TaskId { get; set; }
    }
}
