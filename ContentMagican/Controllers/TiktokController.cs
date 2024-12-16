using ContentMagican.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class TiktokController : Controller
    {
        TiktokService _tiktokService;

        public TiktokController(TiktokService tiktokService)
        {
            _tiktokService = tiktokService;
        }

     

        [HttpGet]
        public async Task<IActionResult> AppAuth()
        {
            var url = await _tiktokService.GenerateAppAuthenticationUrl(
                //Url.Action("Main", "Dashboard", Request.Scheme)
"https://www.google.se/?hl=sv"
                );
            return Redirect(url);
        }

        [HttpPost]
        public async Task<IActionResult> Auth()
        {
           
            return RedirectToAction("ChangeTaskSettings", "Tasks");
        }
    }
}
