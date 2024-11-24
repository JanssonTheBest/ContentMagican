using System.Runtime.CompilerServices;

namespace ContentMagican.Services
{
    public class FFmpegService
    {
        public FFmpegService() { }

        public async Task<string> CreateFFmpegStringFromParameters(bool verticalResolution, string textStyle, string gameplayVideo, string videoTitle)
        {
            string ffmpegString = "ffmpeg -i \"C:\\Users\\chfzs\\Desktop\\bin\\Media\\MinecraftGameplay.mp4\" -i \"C:\\Users\\chfzs\\Desktop\\bin\\Media\\Creepy.mp3\" -ss 00:00:00 -t 00:01:00 -filter_complex \"[0:v]crop=in_h*9/16:in_h:(in_w-(in_h*9/16))/2:0,drawtext=text='Your Text Here':fontcolor=white:fontsize=48:x=(w-text_w)/2:y=(h-text_h)/2[v]\" -map \"[v]\" -map 1:a -c:v libx264 -c:a aac -shortest \"C:\\Users\\chfzs\\Desktop\\bin\\Media\\output_combined_with_text_phone.mp4\"\r\n";
            return ffmpegString;
        }
    }
}
