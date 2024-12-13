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
            string outputPath)
        {
            if (!File.Exists(backgroundAudioPath) || !File.Exists(backgroundVideoPath))
            {
                throw new FileNotFoundException("Background audio or video file not found.");
            }

            string tempAudioPath = Path.Combine(Path.GetTempPath(), "temp_tts_audio.mp3");
            string tempSubtitlePath = Path.Combine(Path.GetTempPath(), "temp_subtitles.srt");

            try
            {
                File.WriteAllBytes(tempAudioPath, textToSpeechBytes);

                GenerateSubtitlesFile(wordTimings, tempSubtitlePath);

                TimeSpan ttsDuration = GetAudioDuration(tempAudioPath);
                string escapedSubtitlePath = tempSubtitlePath.Replace("\\", "\\\\").Replace(":", "\\:");
                string command = $"-stream_loop -1 -i \"{backgroundVideoPath}\" -i \"{backgroundAudioPath}\" -i \"{tempAudioPath}\" " +
                          $"-filter_complex \"[0:v]crop=ih*(9/16):ih:(iw-ih*(9/16))/2:0,subtitles='{escapedSubtitlePath}':force_style='Alignment=10,Outline=1'[v];" +
                          $"[1:a]volume=0.2[a1];[2:a]volume=1.7[a2];" +
                          $"[a1][a2]amix=inputs=2[a]\" " +
                          $"-map \"[v]\" -map \"[a]\" -t {ttsDuration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                          $"-c:v libx264 -crf 23 -preset medium -c:a aac -b:a 192k \"{outputPath}\"";



                RunFfmpeg(command);
            }
            finally
            {
                if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
                if (File.Exists(tempSubtitlePath)) File.Delete(tempSubtitlePath);
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
