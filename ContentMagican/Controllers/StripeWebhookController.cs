using ContentMagican.Database;
using ContentMagican.Repositories;
using ContentMagican.Services;
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
        public async Task<IActionResult> Payment(string id)
        {
            var result = _applicationDbContext.Orders.Where(a => a.SessionId == id).FirstOrDefault();
            if (result == default)
            {
                return Unauthorized();
            }
            var user = await _userService.RetrieveUserInformation(HttpContext);
            if (result.UserId == user.Id)
            {
                var userchange = _applicationDbContext.Users.Where(a => a.Id == user.Id).FirstOrDefault();
                userchange.PlanId = result.ProductId;
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(id);
                userchange.CustomerId = session.CustomerId;
                result.Status = "success";
                await _applicationDbContext.SaveChangesAsync();
                return Ok();
            }
            return Unauthorized();
        }
    }
}
