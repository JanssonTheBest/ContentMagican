using ContentMagican.Database;
using ContentMagican.DTOs;
using ContentMagican.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ContentMagican.Controllers
{
    public class TiktokController : Controller
    {
        TiktokService _tiktokService;
        UserService _userService;
        ApplicationDbContext _context;
        public TiktokController(TiktokService tiktokService, IConfiguration config, UserService userService, ApplicationDbContext context)
        {
            _tiktokService = tiktokService;
            _userService = userService;
            _context = context;
        }



        [HttpGet]
        public async Task<IActionResult> AppAuth()
        {
            var uri = Url.Action("Auth", "Tiktok", null, Request.Scheme);
            var url = await _tiktokService.GenerateAppAuthenticationUrl(
                uri
                );
            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> Auth(string code, string scopes, string state, string error, string error_description)
        {
            var uri = Url.Action("Auth", "Tiktok", null, Request.Scheme);
            var token = await _tiktokService.GetTiktokAccessToken(
                uri
                , code);
            if (token == null)
            {
                return BadRequest("Failed to create token");
            }

            try
            {
                var user = await _userService.RetrieveUserInformation(HttpContext);
                var username = await _tiktokService.GetUserInfo(token.access_token);

                var socialMediaAccesSession = token.ToSocialMediaAccessSession(user.Id, username);

                var entity = _context.SocialMediaAccessSessions.Where(a => a.TiktokUserId == socialMediaAccesSession.TiktokUserId);

                if (socialMediaAccesSession.userId != user.Id)
                {
                    return BadRequest("A conjurecontent-user is already associated with the account provided.\n Login to the relevant conjurecontent-account or clear cookies in your browser, and try adding another user.");
                }

                if (entity.Any() && entity.First().status == 0)
                {
                    var dto = entity.First();
                    dto.date_expires = socialMediaAccesSession.date_expires;
                    dto.refreshtoken = socialMediaAccesSession.refreshtoken;
                    dto.granttype = socialMediaAccesSession.granttype;
                    dto.accesstoken = socialMediaAccesSession.accesstoken;
                    dto.AvatarUrl = socialMediaAccesSession.AvatarUrl;
                    dto.CreatedAt = socialMediaAccesSession.CreatedAt;
                    dto.userId = socialMediaAccesSession.userId;
                    dto.TiktokUserId = socialMediaAccesSession.TiktokUserId;
                    dto.UserName = socialMediaAccesSession.UserName;
                    dto.socialmedia_name = socialMediaAccesSession.socialmedia_name;
                }
                else
                {
                    _context.SocialMediaAccessSessions.Add(socialMediaAccesSession);
                }
                await _context.SaveChangesAsync();

                return RedirectToAction("Main", "Dashboard");

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message + "\n" + ex.InnerException);
            }
        }
    }
}
