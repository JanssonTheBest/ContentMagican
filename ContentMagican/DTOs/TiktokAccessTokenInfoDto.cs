using ContentMagican.Database;
using ContentMagican.Services;

namespace ContentMagican.DTOs
{
    public class TiktokAccessTokenInfoDto
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
        public string open_id { get; set; }
        public int refresh_expires_in { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
        public string token_type { get; set; }

        public SocialMediaAccessSession ToSocialMediaAccessSession(int userId, TikTokUser tikTokUser)
        {
            return new SocialMediaAccessSession()
            {
                accesstoken = access_token,
                date_expires = DateTime.Now.AddSeconds(expires_in),
                granttype = "refresh_token",
                refreshtoken = refresh_token,
                socialmedia_name = "tiktok",
                userId = userId,
                CreatedAt = DateTime.Now,
                UserName = tikTokUser.DisplayName,
                TiktokUserId = tikTokUser.TiktokUserId,
                AvatarUrl = tikTokUser.AvatarUrl,
                status = 0
            };
        }
    }
}
