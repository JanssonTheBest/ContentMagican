using ContentMagican.Database;
using ContentMagican.DTOs;

namespace ContentMagican.Models
{
    public class RedditVideoAutomationSettingsViewModel
    {
        public RedditVideoAutomationSettingsViewModel()
        {

                
        }

        public List<SocialMediaAccessSession> accounts = new List<SocialMediaAccessSession>();


        public List<FontDto> fonts = new List<FontDto>();
        public List<AudioResourceDto> audio = new List<AudioResourceDto>();
        public List<VideoResourceDto> video = new List<VideoResourceDto>();

    }
}
