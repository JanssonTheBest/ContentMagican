using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ContentMagican.Services
{
    public class FFmpegService
    {
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;

        public FFmpegService()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            _ffmpegPath = Path.Combine(baseDir, "ffmpeg", GetExecutableName("ffmpeg"));
            _ffprobePath = Path.Combine(baseDir, "ffmpeg", GetExecutableName("ffprobe"));
        }

        private string GetExecutableName(string name)
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? $"{name}.exe" : name;
        }




        public void CreateVideoWithSubtitles(
       string backgroundAudioPath,
       string backgroundVideoPath,
       byte[] textToSpeechBytes,
       List<(string Word, TimeSpan Timestamp)> wordTimings,
       string outputPath,
       bool useHardwareAcceleration = false,
            double ttsVolume = 1,
            double backgroundAudioVolume = 1)
        {
            if (!File.Exists(backgroundAudioPath) || !File.Exists(backgroundVideoPath))
            {
                throw new FileNotFoundException("Background audio or video file not found.");
            }

            string tempAudioPath = Path.Combine(Path.GetTempPath(), "temp_tts_audio.mp3");
            string tempSubtitlePath = Path.Combine(Path.GetTempPath(), "temp_subtitles.srt");

            try
            {
                Console.WriteLine("FFMPEG creating video");
                File.WriteAllBytes(tempAudioPath, textToSpeechBytes);

                // Generate the .srt subtitle file from your word timings.
                GenerateSubtitlesFile(wordTimings, tempSubtitlePath);

                // Get the TTS audio duration and background video duration.
                TimeSpan ttsDuration = GetAudioDuration(tempAudioPath);
                TimeSpan backgroundDuration = GetVideoDuration(backgroundVideoPath);

                // Choose a random offset from 0 up to the background video length.
                // If you don't mind starting anywhere, even beyond the video length,
                // you could remove the clamp, but this approach avoids weird skipping.
                Random rnd = new Random();
                double maxOffset = Math.Max(0, backgroundDuration.TotalSeconds);
                double randomOffsetSeconds = rnd.NextDouble() * maxOffset;
                TimeSpan randomOffset = TimeSpan.FromSeconds(randomOffsetSeconds);

                // Escape the subtitle path so FFMpeg can read it properly (handle Windows backslashes).
                string escapedSubtitlePath = tempSubtitlePath
                    .Replace("\\", "\\\\")
                    .Replace(":", "\\:");

                // Default software encoding (CPU-based):
                string videoCodec = "-c:v libx264";
                string hwaccel = "";

                // If you have an NVIDIA GPU, you can enable hardware acceleration:
                if (useHardwareAcceleration)
                {
                    // For NVIDIA CUDA NVENC:
                    hwaccel = "-hwaccel cuda";
                    videoCodec = "-c:v h264_nvenc -preset p4";
                    // Adjust presets or add other parameters as needed.
                }

                // Note the order: we first do -stream_loop, then -ss <offset>, then -i <videoPath>.
                // This ensures we seek in the first loop iteration to randomOffset seconds.
                string command =
                    $"{hwaccel} " +
                    $"-stream_loop -1 -ss {randomOffset.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"-i \"{backgroundVideoPath}\" " +
                    $"-i \"{backgroundAudioPath}\" " +
                    $"-i \"{tempAudioPath}\" " +
                    $"-filter_complex \"[0:v]" +
                      // Crop the video to 9:16 vertical if desired (adjust as needed).
                      $"crop=ih*(9/16):ih:(iw-ih*(9/16))/2:0," +
                      // Add subtitles.
                      $"subtitles='{escapedSubtitlePath}':force_style='Alignment=10,Outline=1'[v];" +
                      // Mix background audio and TTS audio at specified volumes.
                      $"[1:a]volume={Convert.ToString(backgroundAudioVolume, CultureInfo.InvariantCulture)}[a1];" +
                      $"[2:a]volume={Convert.ToString(ttsVolume, CultureInfo.InvariantCulture)}[a2];" +
                      $"[a1][a2]amix=inputs=2[a]\" " +
                    // Map our filtered video and mixed audio.
                    $"-map \"[v]\" -map \"[a]\" " +
                    // Cut final output to the TTS duration.
                    $"-t {ttsDuration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    // Apply chosen video codec.
                    $"{videoCodec} " +
                    // Apply audio codec.
                    $"-c:a aac -b:a 192k " +
                    // Final output path.
                    $"\"{outputPath}\"";

                // Run the ffmpeg command.
                RunFfmpeg(command);
            }
            finally
            {
                // Clean up temporary files.
                if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
                if (File.Exists(tempSubtitlePath)) File.Delete(tempSubtitlePath);
            }
        }

        public TimeSpan GetVideoDuration(string videoPath)
        {
            if (!File.Exists(videoPath))
            {
                throw new FileNotFoundException($"Video file not found: {videoPath}");
            }

            // 1) Create a process to call FFprobe
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // 2) Execute ffprobe and read its output
            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // 3) The output is usually just a floating-point number of seconds, e.g. "12.3456"
                if (double.TryParse(result.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
                else
                {
                    // If ffprobe failed to parse the duration, return 0 or throw
                    return TimeSpan.Zero;
                }
            }
        }

        private TimeSpan GetAudioDuration(string audioPath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-i \"{audioPath}\" -show_entries format=duration -v quiet -of csv=\"p=0\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (!double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double durationSeconds))
            {
                throw new Exception($"Unable to parse duration from FFprobe output: {output}");
            }

            return TimeSpan.FromSeconds(durationSeconds);
        }

        private void GenerateSubtitlesFile(List<(string Word, TimeSpan Timestamp)> wordTimings, string subtitlePath)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < wordTimings.Count; i++)
            {
                var start = wordTimings[i].Timestamp;
                var end = i < wordTimings.Count - 1 ? wordTimings[i + 1].Timestamp : start + TimeSpan.FromSeconds(2);

                sb.AppendLine((i + 1).ToString());
                sb.AppendLine($"{FormatTimestamp(start)} --> {FormatTimestamp(end)}");
                sb.AppendLine(wordTimings[i].Word);
                sb.AppendLine();
            }

            File.WriteAllText(subtitlePath, sb.ToString());
        }

        private string FormatTimestamp(TimeSpan timestamp)
        {
            return $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2},{timestamp.Milliseconds:D3}";
        }

        private void RunFfmpeg(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg exited with code {process.ExitCode}: {output}");
            }
        }

    }
}
