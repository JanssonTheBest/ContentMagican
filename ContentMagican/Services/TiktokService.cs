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
        //   private async Task<InitUploadResponse> InitializeUploadAsync(
        //string accessToken,
        //string videoFilePath,
        //string title)
        //   {
        //       try
        //       {
        //           _logger.LogInformation("Initializing direct video upload for posting.");

        //           _httpClient.DefaultRequestHeaders.Authorization =
        //               new AuthenticationHeaderValue("Bearer", accessToken);
        //           _httpClient.DefaultRequestHeaders.Accept.Add(
        //               new MediaTypeWithQualityHeaderValue("application/json"));

        //           FileInfo fileInfo = new FileInfo(videoFilePath);
        //           long videoSize = fileInfo.Length;

        //           // Decide chunkSize
        //           long minChunk = 5L * 1024L * 1024L;  // 5 MB
        //           long maxChunk = 64L * 1024L * 1024L; // 64 MB

        //           long chunkSize;
        //           if (videoSize < minChunk)
        //           {
        //               // Entire file < 5 MB => 1 chunk
        //               chunkSize = videoSize;
        //           }
        //           else
        //           {
        //               // E.g. 10 MB, within 5 ~ 64 MB range
        //               chunkSize = Math.Min(10L * 1024L * 1024L, maxChunk);
        //           }

        //           // Calculate how many full-size chunks, plus leftover remainder
        //           long count = videoSize / chunkSize;  // integer division => floor
        //           long leftover = videoSize % chunkSize;

        //           // We do NOT forcibly do +1. Instead, the final chunk can be "chunkSize + leftover"
        //           // if leftover > 0. But the total "chunk count" from TikTok’s perspective is 'count'.
        //           // If leftover == 0 => exactly 'count' chunks. If leftover > 0 => also 'count' chunks,
        //           // but the last chunk is bigger than chunkSize.
        //           // If chunkSize > videoSize => fallback
        //           if (count == 0)
        //           {
        //               count = 1;       // entire file in 1 chunk
        //               chunkSize = videoSize;
        //               leftover = 0;
        //           }

        //           // This is the tricky part: TikTok wants total_chunk_count = floor(...) for the /init/ call
        //           // so we send 'count' as the total_chunk_count
        //           int totalChunkCount = (int)count;

        //           var requestBody = new
        //           {
        //               post_info = new
        //               {
        //                   title = title,
        //                   privacy_level = "SELF_ONLY"
        //               },
        //               source_info = new
        //               {
        //                   source = "FILE_UPLOAD",
        //                   video_size = videoSize,
        //                   chunk_size = chunkSize,
        //                   total_chunk_count = totalChunkCount
        //               }
        //           };

        //           var response = await _httpClient.PostAsJsonAsync(
        //               "https://open.tiktokapis.com/v2/post/publish/video/init/",
        //               requestBody
        //           );

        //           _logger.LogInformation($"Direct post initialization response status: {response.StatusCode}");
        //           string responseContent = await response.Content.ReadAsStringAsync();
        //           _logger.LogDebug($"Direct post initialization response body: {responseContent}");

        //           if (!response.IsSuccessStatusCode)
        //           {
        //               _logger.LogError($"Failed to initialize direct post: {responseContent}");
        //               throw new Exception($"Initialization failed: {responseContent}");
        //           }

        //           return JsonSerializer.Deserialize<InitUploadResponse>(responseContent);
        //       }
        //       catch (Exception ex)
        //       {
        //           _logger.LogError(ex, "Exception during direct post initialization.");
        //           throw;
        //       }
        //   }

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

                FileInfo fileInfo = new FileInfo(videoFilePath);
                long videoSize = fileInfo.Length;

                // TikTok constraints
                long minChunk = 5L * 1024L * 1024L;  // 5 MB
                long maxChunk = 64L * 1024L * 1024L; // 64 MB

                long chunkSize;
                int totalChunkCount;

                if (videoSize <= minChunk)
                {
                    // Entire file < 5 MB => single-chunk upload
                    chunkSize = videoSize;
                    totalChunkCount = 1;
                }
                else if (videoSize <= maxChunk)
                {
                    // For 5..64 MB, pick a base chunk of 5 MB (or 10 MB if you prefer)
                    chunkSize = minChunk; // 5 MB
                    long count = videoSize / chunkSize;  // floor
                    long leftover = videoSize % chunkSize;

                    if (count == 0)
                    {
                        // Edge case: if the file is slightly >5MB but rounding
                        // leads to 0, fallback to single-chunk
                        chunkSize = videoSize;
                        totalChunkCount = 1;
                    }
                    else
                    {
                        // leftover merges into chunk #count (the final chunk)
                        totalChunkCount = (int)count;
                    }
                }
                else
                {
                    // For >64 MB, chunkSize = 64 MB
                    chunkSize = maxChunk;
                    long count = videoSize / chunkSize;  // floor
                    long leftover = videoSize % chunkSize;

                    if (count == 0)
                    {
                        // Should be rare, but if the file is just barely above 64MB,
                        // and integer division yields 0, fallback
                        chunkSize = videoSize;
                        totalChunkCount = 1;
                    }
                    else
                    {
                        totalChunkCount = (int)count;
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
                    requestBody);

                _logger.LogInformation($"Init response status: {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Init response body: {responseContent}");

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
        //private async Task<bool> UploadVideoFileAsync(string uploadUrl, string videoFilePath)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Uploading video file.");

        //        FileInfo fileInfo = new FileInfo(videoFilePath);
        //        long fileSize = fileInfo.Length;

        //        // Same chunk sizing logic
        //        long minChunk = 5L * 1024L * 1024L;   // 5 MB
        //        long maxChunk = 64L * 1024L * 1024L;  // 64 MB

        //        long chunkSize;
        //        if (fileSize < minChunk)
        //        {
        //            chunkSize = fileSize;
        //        }
        //        else
        //        {
        //            chunkSize = Math.Min(10L * 1024L * 1024L, maxChunk);
        //        }

        //        long count = fileSize / chunkSize;   // floor
        //        long leftover = fileSize % chunkSize;

        //        if (count == 0)
        //        {
        //            count = 1;
        //            chunkSize = fileSize;
        //            leftover = 0;
        //        }

        //        // We'll do 'count' iterations
        //        // In the final iteration, if leftover > 0, we read chunkSize + leftover
        //        int totalChunkCount = (int)count;

        //        using var localClient = new HttpClient();
        //        using var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read);

        //        long totalBytesRead = 0;

        //        for (int chunkIndex = 1; chunkIndex <= totalChunkCount; chunkIndex++)
        //        {
        //            // For chunk 1..(count-1), just read 'chunkSize'
        //            // For chunk == count (the final chunk), if leftover>0 => read chunkSize+leftover
        //            long normalChunk = chunkSize;
        //            // Are we on the final chunk?
        //            bool isFinalChunk = (chunkIndex == totalChunkCount);

        //            if (isFinalChunk && leftover > 0)
        //            {
        //                // final chunk size = chunkSize + leftover
        //                normalChunk += leftover;
        //            }

        //            long bytesLeft = fileSize - totalBytesRead;
        //            // If by some chance there's no more data, break
        //            if (bytesLeft <= 0)
        //            {
        //                break;
        //            }

        //            // We can't read more than what's actually left
        //            long thisChunkSize = Math.Min(bytesLeft, normalChunk);

        //            // Allocate buffer
        //            byte[] buffer = new byte[thisChunkSize];

        //            int bytesRead = await fileStream.ReadAsync(buffer, 0, (int)thisChunkSize);
        //            if (bytesRead != thisChunkSize)
        //            {
        //                throw new InvalidOperationException($"Did not read {thisChunkSize} bytes from file.");
        //            }

        //            long startByte = totalBytesRead;
        //            long endByte = totalBytesRead + bytesRead - 1;

        //            using var content = new ByteArrayContent(buffer, 0, bytesRead);
        //            content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        //            content.Headers.ContentRange = new ContentRangeHeaderValue(startByte, endByte, fileSize)
        //            {
        //                Unit = "bytes"
        //            };

        //            var response = await localClient.PutAsync(uploadUrl, content);
        //            string responseBody = await response.Content.ReadAsStringAsync();

        //            if (!response.IsSuccessStatusCode)
        //            {
        //                _logger.LogError(
        //                    $"Failed to upload chunk {chunkIndex}/{totalChunkCount}. " +
        //                    $"StatusCode={response.StatusCode}, Body={responseBody}"
        //                );
        //                return false;
        //            }

        //            totalBytesRead += bytesRead;

        //            _logger.LogInformation(
        //                $"Chunk {chunkIndex}/{totalChunkCount} uploaded successfully. {bytesRead} bytes sent."
        //            );
        //        }

        //        _logger.LogInformation("All chunks uploaded successfully.");
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Exception during video file upload.");
        //        return false;
        //    }
        //}

        private async Task<bool> UploadVideoFileAsync(string uploadUrl, string videoFilePath)
        {
            try
            {
                _logger.LogInformation("Uploading video file in chunks...");

                FileInfo fileInfo = new FileInfo(videoFilePath);
                long fileSize = fileInfo.Length;

                // Must match the same chunk sizing logic used during /init/
                long minChunk = 5L * 1024L * 1024L;   // 5 MB
                long maxChunk = 64L * 1024L * 1024L;  // 64 MB

                long chunkSize;
                if (fileSize <= minChunk)
                {
                    chunkSize = fileSize;
                }
                else if (fileSize <= maxChunk)
                {
                    chunkSize = minChunk; // 5 MB
                }
                else
                {
                    chunkSize = maxChunk; // 64 MB
                }

                long count = fileSize / chunkSize;   // floor
                long leftover = fileSize % chunkSize;

                // If count==0, fallback to single-chunk
                if (count == 0)
                {
                    count = 1;
                    chunkSize = fileSize;
                    leftover = 0;
                }

                // The final chunk will be chunkSize + leftover if leftover>0
                int totalChunkCount = (int)count;
                _logger.LogInformation($"File is {fileSize} bytes. Using chunkSize={chunkSize} for base chunk, leftover={leftover}, totalChunkCount={totalChunkCount}");

                long totalBytesRead = 0;

                using var localClient = new HttpClient();
                using var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read);

                for (int chunkIndex = 1; chunkIndex <= totalChunkCount; chunkIndex++)
                {
                    // For chunk 1..(count-1), read exactly chunkSize bytes
                    // For the final chunk, if leftover>0, read chunkSize + leftover
                    bool isFinalChunk = (chunkIndex == totalChunkCount);
                    long thisChunkSize = chunkSize;

                    if (isFinalChunk && leftover > 0)
                    {
                        thisChunkSize += leftover;
                    }

                    // But do not exceed what's actually left
                    long bytesRemaining = fileSize - totalBytesRead;
                    if (bytesRemaining < thisChunkSize)
                    {
                        thisChunkSize = bytesRemaining;
                    }

                    // Read from file
                    byte[] buffer = new byte[thisChunkSize];
                    int bytesRead = await fileStream.ReadAsync(buffer, 0, (int)thisChunkSize);
                    if (bytesRead != thisChunkSize)
                    {
                        throw new InvalidOperationException($"Did not read the expected {thisChunkSize} bytes from file.");
                    }

                    long startByte = totalBytesRead;          // inclusive
                    long endByte = totalBytesRead + bytesRead - 1; // inclusive
                    long totalByteLength = fileSize;          // the final “/total” in Content-Range

                    // Debug logs to see exactly what we send
                    _logger.LogDebug($"Uploading chunkIndex={chunkIndex}, " +
                                     $"startByte={startByte}, endByte={endByte}, " +
                                     $"chunkSize={thisChunkSize} (bytesRead={bytesRead}), " +
                                     $"fileSize={fileSize}.");

                    using var content = new ByteArrayContent(buffer, 0, bytesRead);
                    // Some devs use "Content-Type: application/octet-stream" or "video/mp4"
                    content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

                    // If your original file is MP4, "video/mp4" is correct.  
                    // If it's a different format, adjust accordingly.

                    content.Headers.ContentRange = new ContentRangeHeaderValue(startByte, endByte, totalByteLength)
                    {
                        Unit = "bytes"
                    };

                    // The server expects chunk #1 from bytes 0..(chunkSize-1), chunk #2 from chunkSize.. etc.
                    var response = await localClient.PutAsync(uploadUrl, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to upload chunk {chunkIndex}/{totalChunkCount}. " +
                                         $"StatusCode={response.StatusCode}, Body={responseBody}");
                        return false;
                    }

                    // If it’s NOT the final chunk, TikTok typically returns 206 (PartialContent).
                    // If it’s the final chunk, TikTok often returns 201 (Created).
                    _logger.LogInformation($"Chunk {chunkIndex}/{totalChunkCount} uploaded successfully. {bytesRead} bytes sent. (HTTP {response.StatusCode})");

                    totalBytesRead += bytesRead;
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


