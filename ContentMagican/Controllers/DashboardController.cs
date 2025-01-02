using ContentMagican.Models;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{

    public class DashboardController : Controller
    {
        TiktokService _tiktokService;
        UserService _userService;
        public DashboardController(TiktokService tiktokService,
UserService userService)
        {
            _tiktokService = tiktokService;
            _userService = userService;
        }

        public async Task<IActionResult> Main()
        {

            var debug = User.Claims;
            var accessSessions = await _userService.RetrieveActiveUserSocialMediaAccessSessions(HttpContext);
            var firstSession = accessSessions.FirstOrDefault();


            if (firstSession == default)
            {
                return View(new DashboardViewModel()
                {

                });

            }

            return View(new DashboardViewModel()
            {
                videoStatsDto = await _tiktokService.GetAllVideoStatsAsync(firstSession.accesstoken),
                userStats = await _tiktokService.GetUserInfo(firstSession.accesstoken),
                socialMediaAccessSessions = accessSessions.ToList()
            });
        }


        [HttpPost]
        public async Task<IActionResult> DisconnectPlatform(int id)
        {
            var success = await _userService.RemoveSocialMediaAccessSession(id, HttpContext);
            return Ok(new { message = "Disconnected successfully." });
        }

        public IActionResult ConnectPlatform(string platform)
        {
            return RedirectToAction("AppAuth", "Tiktok");
        }
    }
}
