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


        public List<AudioResourceDto> audio = new List<AudioResourceDto>();
        public List<VideoResourceDto> video = new List<VideoResourceDto>();

    }
}
