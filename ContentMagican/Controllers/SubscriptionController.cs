using ContentMagican.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ContentMagican.Controllers
{
    public class SubscriptionController : Controller
    {
        StripeService _stripeService;
        public SubscriptionController(StripeService stripeService)
        {
            _stripeService = stripeService;
        }

        [HttpGet]
        public  async Task<IActionResult> Subscribe(long userId, string subscriptionId)
        {
            string url = await _stripeService.StripeSession(userId,subscriptionId, Url.Action("Main", "Dashboard", null, Request.Scheme));

//#if (DEBUG)
//            return RedirectToAction("Payment","StripeWebhook",new {id = "cs_test_a1tRwkSzrhQuNgntHSScFkFSzDb93FXpXD1YTEW5NK43hngtWoSVDMCEfQ" });
//#endif

            return Redirect(url);
        }
    }
}
