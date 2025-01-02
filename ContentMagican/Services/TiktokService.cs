using ContentMagican.Database;
using ContentMagican.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

                string fields = "display_name,open_id,avatar_url,follower_count,following_count,likes_count,";
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



        public async Task<VideoStatsDto> GetVideoStatsAsync(string accessToken, string videoId)
        {
            try
            {
                _logger.LogInformation("Retrieving video statistics for video ID: {videoId}", videoId);

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                // NOTE: TikTok docs no longer show a direct "GET video stats" endpoint for v2,
                //       so if this is a legacy/older endpoint, you can keep it, or else
                //       you'd use the /v2/video/query/ endpoint to fetch a single ID’s stats.
                var response = await _httpClient.GetAsync($"https://open.tiktokapis.com/v2/video/stats/?video_id={videoId}");

                _logger.LogInformation("Video stats response status: {statusCode}", response.StatusCode);
                var responseData = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Video stats response: {responseData}", responseData);

                response.EnsureSuccessStatusCode();
                return JsonSerializer.Deserialize<VideoStatsDto>(responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while retrieving TikTok video statistics.");
                throw;
            }
        }

        /// <summary>
        ///  Get stats for **all** videos posted in the last 7 days (including current like_count, view_count, etc.).
        /// </summary>
        public async Task<List<VideoStatsDto>> GetAllVideoStatsAsync(string accessToken)
        {
            try
            {
                _logger.LogInformation("Retrieving all video statistics from the last 7 days...");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var videoStatsList = new List<VideoStatsDto>();

                // For pagination: the cursor is a long (UTC epoch in ms) from TikTok.
                // We'll pass it in the request body, and read back from the response.
                long? cursor = null;
                bool hasMore = true;

                // We'll stop either when TikTok indicates "no more pages" (has_more == false)
                // or once all the videos we get are older than 7 days.
                while (hasMore)
                {
                    // Prepare the request body for the POST /v2/video/list/ endpoint
                    var requestBody = new
                    {
                        cursor = cursor,     // The last page's cursor (may be null first time)
                        max_count = 20       // The maximum number of videos per page (<= 20)
                    };

                    // We also add desired fields as per TikTok docs:
                    // e.g., "?fields=id,create_time,like_count,comment_count,share_count,view_count"
                    var url = "https://open.tiktokapis.com/v2/video/list/?fields=id,create_time,like_count,comment_count,share_count,view_count";

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();

                    var responseData = await response.Content.ReadAsStringAsync();
                    var videoListResponse = JsonSerializer.Deserialize<VideoListResponse>(responseData);

                    if (videoListResponse?.Data?.Videos == null || !videoListResponse.Data.Videos.Any())
                    {
                        _logger.LogWarning("No videos found in this page of data.");
                        break;
                    }

                    // Filter to only keep videos posted within the last 7 days:
                    var oneWeekAgoUnix = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();

                    // Because create_time is in seconds, compare directly with oneWeekAgoUnix
                    var recentVideos = videoListResponse.Data.Videos
                        .Where(v => v.CreateTime >= oneWeekAgoUnix)
                        .ToList();

                    // If none of the videos in this batch are from last 7 days, break.
                    if (!recentVideos.Any())
                    {
                        _logger.LogInformation("All videos in this page are older than 7 days.");
                        break;
                    }

                    // 2) Query the details (including like/view counts) for these recent videos
                    var videoIds = recentVideos.Select(v => v.VideoId).ToList();
                    var videoDetailsResponse = await QueryVideoDetailsAsync(accessToken, videoIds);

                    if (videoDetailsResponse?.Data == null || !videoDetailsResponse.Data.Any())
                    {
                        _logger.LogWarning("No video details returned for the recent videos.");
                        break;
                    }

                    videoStatsList.AddRange(videoDetailsResponse.Data);

                    // Pagination: update cursor & hasMore from the current response
                    // The TikTok docs say "has_more" is in the top-level data object
                    // to indicate if there's another page. If "has_more" is false,
                    // you can stop. If it's true, use "cursor" to keep paginating.
                    cursor = videoListResponse.Data.Cursor;    // e.g. 1643332803000
                    hasMore = videoListResponse.Data.HasMore;
                }

                _logger.LogInformation("Retrieved statistics for all videos (posted in last 7 days).");
                return videoStatsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while retrieving all video statistics.");
                throw;
            }
        }

        /// <summary>
        ///  Calls TikTok’s /v2/video/query/ to fetch the details of up to 20 video IDs.
        ///  This returns current like_count, view_count, share_count, etc.
        /// </summary>
        private async Task<VideoDetailsResponse> QueryVideoDetailsAsync(string accessToken, List<string> videoIds)
        {
            // Per TikTok docs, we do a POST to:
            // https://open.tiktokapis.com/v2/video/query/?fields=id,create_time,...
            // And in the body:
            // {
            //   "filters": {
            //       "video_ids": ["id1","id2",...]
            //   }
            // }
            // You can specify any fields you want in the query string.

            var url = "https://open.tiktokapis.com/v2/video/query/?fields=id,like_count,comment_count,share_count,view_count,duration";

            var bodyObject = new
            {
                filters = new
                {
                    video_ids = videoIds
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(bodyObject), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<VideoDetailsResponse>(responseData);
        }


    }


    public class VideoListResponse
    {
        [JsonPropertyName("data")]
        public VideoListData Data { get; set; }

        [JsonPropertyName("error")]
        public Error Error { get; set; }
    }

    public class VideoListData
    {
        [JsonPropertyName("videos")]
        public List<VideoInfo> Videos { get; set; }

        [JsonPropertyName("cursor")]
        public long? Cursor { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    public class VideoInfo
    {
        [JsonPropertyName("id")]
        public string VideoId { get; set; }

        // Create time is a Unix epoch (seconds) for v2 (per docs).
        [JsonPropertyName("create_time")]
        public long CreateTime { get; set; }

        // If you request these in ?fields=like_count,comment_count, etc. then you can also
        // deserialize them here. For simplicity, we usually rely on QueryVideoDetailsAsync though.
        [JsonPropertyName("like_count")]
        public int? LikeCount { get; set; }

        [JsonPropertyName("view_count")]
        public long? ViewCount { get; set; }

        // etc.
    }

    public class VideoDetailsResponse
    {
        [JsonPropertyName("data")]
        public List<VideoStatsDto> Data { get; set; }

        [JsonPropertyName("error")]
        public Error Error { get; set; }
    }

    public class VideoStatsDto
    {
        [JsonPropertyName("id")]
        public string VideoId { get; set; }

        [JsonPropertyName("like_count")]
        public int LikeCount { get; set; }

        [JsonPropertyName("view_count")]
        public long ViewCount { get; set; }

        [JsonPropertyName("comment_count")]
        public int CommentCount { get; set; }

        [JsonPropertyName("share_count")]
        public int ShareCount { get; set; }

        // Add any other relevant fields you request
    }


    public class UserStatsDto
    {
        [JsonPropertyName("data")]
        public UserStatsData Data { get; set; }

        [JsonPropertyName("error")]
        public Error Error { get; set; }
    }

    public class UserStatsData
    {
        [JsonPropertyName("follower_count")]
        public int FollowerCount { get; set; }

        [JsonPropertyName("following_count")]
        public int FollowingCount { get; set; }
    }

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

        [JsonPropertyName("follower_count")]
        public long FollowerCount { get; set; }

        [JsonPropertyName("following_count")]
        public long FollowingCount { get; set; }

        [JsonPropertyName("likes_count")]
        public long LikesCount { get; set; }
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


