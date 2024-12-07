using ContentMagican.Repositories;
using ContentMagican.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ContentMagican.Controllers
{
    public class SubscriptionController : Controller
    {
        StripeService _stripeService;
        StripeRepository _stripeRepository;
        UserService _userService;
        public SubscriptionController(StripeService stripeService, StripeRepository stripeRepository, UserService userService)
        {
            _stripeService = stripeService;
            _stripeRepository = stripeRepository;
            _userService = userService;
        }

        [HttpGet]
        public  async Task<IActionResult> Subscribe(long userId, string subscriptionId)
        {
            await _stripeRepository.GetCustomerByEmailAsync((await _userService.RetrieveUserInformation(HttpContext)).Email);
            string url = await _stripeService.StripeSession(userId,subscriptionId, Url.Action("Main", "Dashboard", null, Request.Scheme),HttpContext);

//#if (DEBUG)
//            return RedirectToAction("Payment","StripeWebhook",new {id = "cs_test_a1tRwkSzrhQuNgntHSScFkFSzDb93FXpXD1YTEW5NK43hngtWoSVDMCEfQ" });
//#endif

            return Redirect(url);
        }
    }
}
