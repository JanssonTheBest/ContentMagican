namespace ContentMagican.Services
{
    public class TiktokService
    {
        private HttpClient _httpClient = new();
        private string _appKey;
        private string _appSecret;
        public TiktokService(IConfiguration configuration)
        {
            var relevantSection = configuration.GetSection("TiktokCredentials");
            _appKey = relevantSection["ClientKey"];
            _appSecret = relevantSection["ClientSecret"];
        }

        public async Task<string> GenerateAppAuthenticationUrl(string redirectUrl)
        {
            var stateId = Guid.NewGuid().ToString().Replace("-", "");
            return $"https://www.tiktok.com/v2/auth/authorize?client_key={_appKey}&response_type=code&scope=user.info.basic,video.publish,video.upload,user.info.stats,video.list&redirect_uri={redirectUrl}&state={stateId}";
        }

    }
}
