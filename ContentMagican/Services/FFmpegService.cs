using ContentMagican.DTOs;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ContentMagican.Services
{
    public class FFmpegService
    {
        public FFmpegService() { }

        public async Task<string> CreateVideoPresetFromParameters(bool verticalResolution, string textStyle, string gameplayVideo)
        {
            var videoPresetJson = JsonSerializer.Serialize(new VideoPresetDto()
            {
                GameplayVideo = gameplayVideo,
                VerticalResolution = verticalResolution,
                TextStyle = textStyle,
            });
            return videoPresetJson;
        }
    }
}
