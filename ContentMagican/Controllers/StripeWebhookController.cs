using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class StripeWebhookController : Controller
    {
        public IActionResult Payment(long client_reference_id)
        {
            return View();
        }
    }
}
