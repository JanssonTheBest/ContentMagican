using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OpenAI.Chat;
using OpenAI;

namespace ContentMagican.Services
{
    public class OpenAIService
    {
        private readonly string _apiKey;
        private readonly string _defaultModel;

        public OpenAIService(IConfiguration configuration)
        {
            _apiKey = configuration.GetSection("OpenAICredentials")["secret"];
            if (string.IsNullOrEmpty(_apiKey))
                throw new ArgumentNullException(nameof(_apiKey), "API key must not be null or empty.");

            //_defaultModel = "gpt-4o-mini"; 
            _defaultModel = "gpt-4o";
        }

        private ChatClient GetChatClient(string modelName)
        {
            var openAiClient = new OpenAIClient(_apiKey);
            return openAiClient.GetChatClient(modelName);
        }

        public async Task<string> AskQuestionAsync(string question, string? modelName = null)
        {
            if (string.IsNullOrEmpty(question))
                throw new ArgumentNullException(nameof(question), "Question must not be null or empty.");

            string modelToUse = modelName ?? _defaultModel;

            var chatClient = GetChatClient(modelToUse);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateUserMessage(ChatMessageContentPart.CreateTextPart(question))
            };

            var response = await chatClient.CompleteChatAsync(messages);

            return response.Value?.Content?.FirstOrDefault().Text ?? "";
        }

        private HttpClient GetHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            return client;
        }

        public async Task<List<(string Word, TimeSpan Start)>> TranscribeMp3Async(byte[] mp3Bytes)
        {
            if (mp3Bytes == null || mp3Bytes.Length == 0)
                throw new ArgumentException("MP3 bytes must not be null or empty.", nameof(mp3Bytes));

            using var httpClient = GetHttpClient();
            using var content = new MultipartFormDataContent();
            using var audioContent = new ByteArrayContent(mp3Bytes);

            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
            content.Add(audioContent, "file", "audio.mp3");

            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("verbose_json"), "response_format");
            content.Add(new StringContent("word"), "timestamp_granularities[]");

            var response = await httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI Whisper API call failed: {error}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var transcriptionResult = JsonSerializer.Deserialize<WhisperTranscriptionResponse>(jsonResponse);

            if (transcriptionResult?.Words == null)
                throw new Exception("Failed to parse transcription response.");

            var result = transcriptionResult.Words.Select(word => (
                Word: word.Word,
                Start: TimeSpan.FromSeconds(word.Start)
            )).ToList();

            return result;
        }

        private class WhisperTranscriptionResponse
        {
            [JsonPropertyName("words")]
            public List<WhisperWord> Words { get; set; }
        }

        private class WhisperWord
        {
            [JsonPropertyName("word")]
            public string Word { get; set; }

            [JsonPropertyName("start")]
            public double Start { get; set; }
        }
    }
}
