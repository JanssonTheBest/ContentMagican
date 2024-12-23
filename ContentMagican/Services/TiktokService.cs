using ContentMagican.Database;
using ContentMagican.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace ContentMagican.Services
{
    public class TiktokService
    {
        private HttpClient _httpClient = new();
        private readonly ILogger<TiktokService> _logger;
        private readonly string _appKey;
        private readonly string _appSecret;

        public TiktokService(IConfiguration configuration, ILogger<TiktokService> logger)
        {
            var relevantSection = configuration.GetSection("TiktokCredentials");
            _appKey = relevantSection["ClientKey"];
            _appSecret = relevantSection["ClientSecret"];
            _logger = logger;
        }

        public async Task<string> GenerateAppAuthenticationUrl(string redirectUrl)
        {
            var stateId = Guid.NewGuid().ToString().Replace("-", "");
            return $"https://www.tiktok.com/v2/auth/authorize?client_key={_appKey}&response_type=code&scope=user.info.basic,video.publish,video.upload,user.info.stats,video.list&redirect_uri={WebUtility.UrlEncode(redirectUrl)}&state={stateId}";
        }

        public async Task<TiktokAccessTokenInfoDto> GetTiktokAccessToken(string redirectUrl, string code)
        {
            try
            {
                _logger.LogInformation("Attempting to get access token.");
                var form = new Dictionary<string, string>()
                {
                    {"client_key", HttpUtility.UrlEncode(_appKey)},
                    {"client_secret", HttpUtility.UrlEncode(_appSecret)},
                    {"code", HttpUtility.UrlEncode(code)},
                    {"grant_type", HttpUtility.UrlEncode("authorization_code")},
                    {"redirect_uri", HttpUtility.UrlEncode(redirectUrl)},
                };

                var result = await _httpClient.PostAsync(
                    "https://open.tiktokapis.com/v2/oauth/token/",
                    new FormUrlEncodedContent(form)
                );

                _logger.LogInformation($"Access token response status: {result.StatusCode}");
                var responseContent = await result.Content.ReadAsStringAsync();
                _logger.LogDebug($"Access token response: {responseContent}");

                if (!result.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get access token: {responseContent}");
                    return null;
                }

                return JsonSerializer.Deserialize<TiktokAccessTokenInfoDto>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while getting TikTok access token.");
                return null;
            }
        }

        public async Task<TikTokUser> GetUserInfo(string accessToken)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve user info.");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string fields = "display_name,open_id,avatar_url";
                var response = await _httpClient.GetAsync($"https://open.tiktokapis.com/v2/user/info/?fields={fields}");

                _logger.LogInformation($"User info response status: {response.StatusCode}");
                var responseData = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"User info response: {responseData}");

                response.EnsureSuccessStatusCode();

                var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(responseData);
                return userInfo?.Data?.User ?? throw new Exception("User info not found in the response.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while retrieving TikTok user info.");
                throw;
            }
        }

        /// <summary>
        /// Upload a video in two steps:
        /// 1) /v2/post/publish/video/init/  => get publishId + uploadUrl
        /// 2) PUT chunks => uploadUrl
        ///    (Optional) /complete step if required by TikTok
        /// </summary>
        public async Task UploadVideoAsync(string accessToken, string videoFilePath, string description, string[] tags)
        {
            try
            {
                _logger.LogInformation("Starting video upload process.");
                string title = description + " " + string.Join(" ", tags);

                // Step 1: init
                var initResponse = await InitializeUploadAsync(accessToken, videoFilePath, title);
                if (initResponse == null || string.IsNullOrEmpty(initResponse.Data.UploadUrl))
                {
                    _logger.LogError("Failed to initialize video upload.");
                    throw new Exception("Failed to initialize video upload.");
                }

                // Step 2: upload chunks
                bool uploadSuccess = await UploadVideoFileAsync(initResponse.Data.UploadUrl, videoFilePath);
                if (!uploadSuccess)
                {
                    _logger.LogError("Failed to upload video file.");
                    throw new Exception("Failed to upload video file.");
                }

                _logger.LogInformation("Video uploaded successfully.");

                // Step 3: (Optional) /v2/post/publish/video/complete/ if needed
                // await CompleteUploadAsync(accessToken, initResponse.PublishId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during video upload process.");
                throw;
            }
        }

        /// <summary>
        /// Step 1: /v2/post/publish/video/init/
        /// 
        /// According to TikTok's doc:
        /// - If video < 5 MB => chunk_size = video_size => total_chunk_count=1
        /// - Otherwise => chunk_size can be 5-64 MB, 
        ///   total_chunk_count = floor(video_size / chunk_size).
        /// </summary>
        private async Task<InitUploadResponse> InitializeUploadAsync(
            string accessToken,
            string videoFilePath,
            string title)
        {
            try
            {
                _logger.LogInformation("Initializing direct video upload for posting.");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var fileInfo = new FileInfo(videoFilePath);
                long videoSize = fileInfo.Length;

                // TikTok rules: min chunk is 5 MB, max chunk is 64 MB, final chunk can exceed chunk_size
                long minChunk = 5L * 1024L * 1024L;
                long maxChunk = 64L * 1024L * 1024L;

                long chunkSize;
                int totalChunkCount;

                if (videoSize < minChunk)
                {
                    // Entire file as one chunk
                    chunkSize = videoSize;
                    totalChunkCount = 1;
                }
                else
                {
                    // For example, choose 10 MB
                    chunkSize = Math.Min(10L * 1024L * 1024L, maxChunk);

                    // Floor division for chunk count => final chunk can be bigger
                    totalChunkCount = (int)(videoSize / chunkSize);

                    // If file is between 5 MB and 10 MB, totalChunkCount might be 0 => fix that
                    if (totalChunkCount == 0)
                    {
                        chunkSize = videoSize;
                        totalChunkCount = 1;
                    }
                }

                var requestBody = new
                {
                    post_info = new
                    {
                        title = title,
                        privacy_level = "SELF_ONLY"
                    },
                    source_info = new
                    {
                        source = "FILE_UPLOAD",
                        video_size = videoSize,
                        chunk_size = chunkSize,
                        total_chunk_count = totalChunkCount
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(
                    "https://open.tiktokapis.com/v2/post/publish/video/init/",
                    requestBody
                );

                _logger.LogInformation($"Direct post initialization response status: {response.StatusCode}");
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Direct post initialization response body: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to initialize direct post: {responseContent}");
                    throw new Exception($"Initialization failed: {responseContent}");
                }

                return JsonSerializer.Deserialize<InitUploadResponse>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during direct post initialization.");
                throw;
            }
        }

        /// <summary>
        /// Step 2: PUT chunks to uploadUrl. 
        /// The final chunk can exceed chunk_size if leftover bytes exist.
        /// </summary>
        private async Task<bool> UploadVideoFileAsync(string uploadUrl, string videoFilePath)
        {
            try
            {
                _logger.LogInformation("Uploading video file.");

                var fileInfo = new FileInfo(videoFilePath);
                long fileSize = fileInfo.Length;

                // Re-do the same chunkSize logic (for consistency).
                long minChunk = 5L * 1024L * 1024L;
                long maxChunk = 64L * 1024L * 1024L;

                long chunkSize;
                if (fileSize < minChunk)
                {
                    chunkSize = fileSize;
                }
                else
                {
                    chunkSize = Math.Min(10L * 1024L * 1024L, maxChunk);
                }

                using (var localClient = new HttpClient())
                using (var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[chunkSize];
                    long totalBytesRead = 0;
                    int chunkIndex = 0;

                    while (true)
                    {
                        int bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead <= 0) break; // done

                        chunkIndex++;
                        long startByte = totalBytesRead;
                        long endByte = totalBytesRead + bytesRead - 1;

                        var content = new ByteArrayContent(buffer, 0, bytesRead);
                        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                        content.Headers.ContentRange = new ContentRangeHeaderValue(
                            startByte,
                            endByte,
                            fileSize
                        )
                        {
                            Unit = "bytes"
                        };

                        var response = await localClient.PutAsync(uploadUrl, content);

                        _logger.LogInformation($"Chunk {chunkIndex} upload response status: {response.StatusCode}");
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogDebug($"Chunk {chunkIndex} upload response: {responseContent}");

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("Failed to upload video chunk.");
                            return false;
                        }

                        totalBytesRead += bytesRead;
                    }
                }

                _logger.LogInformation("All chunks uploaded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during video file upload.");
                return false;
            }
        }

        // (OPTIONAL) Step 3: if needed
        /*
        private async Task CompleteUploadAsync(string accessToken, string publishId)
        {
            try
            {
                _logger.LogInformation("Completing the video upload.");
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var requestBody = new { publish_id = publishId };
                var response = await _httpClient.PostAsJsonAsync(
                    "https://open.tiktokapis.com/v2/post/publish/video/complete/",
                    requestBody
                );

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Complete call response: {response.StatusCode}, body: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to complete the upload. {responseContent}");
                }

                _logger.LogInformation("Video upload completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during finalizing the upload.");
                throw;
            }
        }
        */
    }

    // ------------------------------------------------------------------------
    // Below are the same classes you had; your TikTokUser class is UNTOUCHED
    // ------------------------------------------------------------------------

    public class InitUploadResponse
    {
        [JsonPropertyName("data")]
        public UploadData Data { get; set; }

        [JsonPropertyName("error")]
        public UploadError Error { get; set; }
    }

    public class UploadData
    {
        [JsonPropertyName("publish_id")]
        public string PublishId { get; set; }

        [JsonPropertyName("upload_url")]
        public string UploadUrl { get; set; }
    }

    public class UploadError
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("log_id")]
        public string LogId { get; set; }
    }


    public class UserInfoResponse
    {
        [JsonPropertyName("data")]
        public Data Data { get; set; }

        [JsonPropertyName("error")]
        public Error Error { get; set; }
    }

    public class Data
    {
        [JsonPropertyName("user")]
        public TikTokUser User { get; set; }
    }

    public class TikTokUser
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("open_id")]
        public string TiktokUserId { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }
    }

    public class Error
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("log_id")]
        public string LogId { get; set; }
    }
}


