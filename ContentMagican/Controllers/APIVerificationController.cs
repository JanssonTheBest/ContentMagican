using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
        [Route("tiktok_verify.txt")]
        public class VerificationController : Controller
        {
            public IActionResult Index()
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "tiktokXsOLE8u4HYO2pcOTRIhcNtrlkkKW6ulr.txt");
                var fileContent = System.IO.File.ReadAllText(filePath);
                return Content(fileContent, "text/plain");
            }
        }
}
