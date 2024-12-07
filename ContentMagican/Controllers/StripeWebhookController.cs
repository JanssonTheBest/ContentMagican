using ContentMagican.Database;
using ContentMagican.Repositories;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace ContentMagican.Controllers
{
    public class StripeWebhookController : Controller
    {
        ApplicationDbContext _applicationDbContext;
        UserService _userService;
        StripeRepository _stripeRepository;
        public StripeWebhookController(ApplicationDbContext applicationDbContext, UserService userService, StripeRepository stripeRepository)
        {
            _applicationDbContext = applicationDbContext;
            _userService = userService;
            _stripeRepository = stripeRepository;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Payment()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var stripeSignature = Request.Headers["Stripe-Signature"];

            Event stripeEvent;

            Session checkoutSession;

            try
            {
                string secret = await _stripeRepository.GetCheckoutWebhookSecret();
                stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, secret);
                checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;
            }
            catch (StripeException e)
            {
                return BadRequest();
            }

            var result = _applicationDbContext.Users.Where(a => a.Id == Convert.ToInt32(checkoutSession.Metadata["UserId"])).FirstOrDefault();

            if (result == default)
            {
                return Unauthorized("User not found");
            }

            try
            {
                result.PlanId = checkoutSession.Metadata["ProductId"];
                await _applicationDbContext.SaveChangesAsync();
            }
            catch
            {
                return BadRequest("Failed saving");
            }

            return Ok(result);
        }
    }
}
