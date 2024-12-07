using ContentMagican.Database;
using ContentMagican.Repositories;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Linq.Expressions;

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
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var stripeSignature = Request.Headers["Stripe-Signature"];

            Event stripeEvent;

            try
            {
                string secret = await _stripeRepository.GetCheckoutWebhookSecret();
                stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, secret);
            }
            catch (StripeException e)
            {
                return BadRequest();
            }

            switch (stripeEvent.Type)
            {
                case EventTypes.CheckoutSessionCompleted:
                    return await HandleCheckout(stripeEvent);
                case EventTypes.CustomerSubscriptionDeleted:

                    break;
            }

            return BadRequest("Didnt match");
        }



        public async Task<IActionResult> HandleCheckout(Event stripeEvent)
        {
            Session checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;

            if(checkoutSession.PaymentStatus != "paid")
            {
                return Ok("Not Paid");
            }

            var result = _applicationDbContext.Users.Where(a => a.Id == Convert.ToInt32(checkoutSession.Metadata["UserId"])).FirstOrDefault();

            if (result == default)
            {
                return BadRequest("User not found");
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


        public async Task<IActionResult> HandleCustomerSubscriptionDeleted(Event stripeEvent)
        {
            return Ok();
        }
    }
}
