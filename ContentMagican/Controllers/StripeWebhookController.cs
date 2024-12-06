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
            string id;
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var stripeSignature = Request.Headers["Stripe-Signature"];

            Event stripeEvent;
          

            try
            {
                string secret = await _stripeRepository.GetCheckoutWebhookSecret();
                stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, secret);
                id = (stripeEvent.Data.Object as Stripe.Checkout.Session).Id;
                
            }
            catch (StripeException e)
            {
                // Invalid signature
                return BadRequest();
            }



            var result = _applicationDbContext.Orders.Where(a => a.SessionId == id).FirstOrDefault();
            if (result == default)
            {
                return Unauthorized($"SHORT DELAY? sessionId tried:{id}");
            }
            var user = await _userService.RetrieveUserInformation(HttpContext);
            if (result.UserId == user.Id)
            {
                var userchange = _applicationDbContext.Users.Where(a => a.Id == user.Id).FirstOrDefault();
                userchange.PlanId = result.ProductId;
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(id);
                userchange.CustomerId = session.CustomerId ?? "error";
                result.Status = "success";
                await _applicationDbContext.SaveChangesAsync();
                return Ok();
            }
            return Unauthorized("WTF");
        }
    }
}
