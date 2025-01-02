using ContentMagican.Database;
using ContentMagican.Services;

namespace ContentMagican.Models
{
    public class DashboardViewModel
    {
        public TikTokUser? userStats;
        public List<VideoStatsDto>? videoStatsDto = new();
        public List<SocialMediaAccessSession>? socialMediaAccessSessions = new();
    }
}
