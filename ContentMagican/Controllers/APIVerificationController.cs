using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
        [Route("tiktok_verify.txt")]
        public class VerificationController : Controller
        {
            public IActionResult Index()
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tiktok8UHXPdJlKE6pK7p699aVNgAZ2pW2hvP6.txt");
                var fileContent = System.IO.File.ReadAllText(filePath);
                return Content(fileContent, "text/plain");
            }
        }
}
