namespace ContentMagican.DTOs
{
    public class ContentInfo
    {
        public string BackgroundAudio { get; set; }
        public string BackgroundVideo { get; set; }
        public string TextToSpeechVoice { get; set; } = "onyx";
        public double BackgroundAudioVolume { get; set; } = 1;
        public double TextToSpeechVolume { get; set; } = 1;
        public string Type { get; set; }
        public string AdditionalInfo { get; set; }
        public string TextStyle { get; set; }
        public double VoiceSpeed { get; set; }
    }
}
