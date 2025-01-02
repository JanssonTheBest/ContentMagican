namespace ContentMagican.Database
{
    public class SocialMediaAccessSession
    {
        public int id { get; set; }
        public int userId { get; set; }
        public string socialmedia_name { get; set; }
        public string granttype { get; set; }
        public string accesstoken { get; set; }
        public string refreshtoken { get; set; }
        public DateTime date_expires { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
        public string TiktokUserId { get; set; }
        public string AvatarUrl { get; set; }
        public int status { get; set; }
    }

}
