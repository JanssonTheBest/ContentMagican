using ContentMagican.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Routing;
using Stripe;
using Stripe.BillingPortal;
namespace ContentMagican.Services
{
    public class StripeService
    {
        ApplicationDbContext _applicationDbContext;
        UserService _userService;
        public StripeService(IConfiguration config, ApplicationDbContext applicationDbContext, UserService userService)
        {
            StripeConfiguration.ApiKey = config.GetSection("StripeCredentials")["sk"];
            _applicationDbContext = applicationDbContext;
            _userService = userService;
        }

        public async Task<string> StripeSession(long userId, string productId, string ridirectUrl, HttpContext ctx)
        {
            var productService = new ProductService();
            var product = await productService.GetAsync(productId);
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                CancelUrl = ridirectUrl,
                SuccessUrl = ridirectUrl,
                CustomerEmail = (await _userService.RetrieveUserInformation(ctx)).Email, // Stripe handles customer creation/retrieval
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                
    {
        new Stripe.Checkout.SessionLineItemOptions
        {
            Price = product.DefaultPriceId,
            Quantity = 1, 
        },
    },
                Mode = "payment",
            };
            var sessionService = new Stripe.Checkout.SessionService();
            var session = await sessionService.CreateAsync(options);
            await _applicationDbContext.Orders.AddAsync(new OrderLog()
            {
                SessionId = session.Id,
                UserId = (int)userId,
                CreatedAt = DateTime.UtcNow,
                ProductId = productId
            });
            await _applicationDbContext.SaveChangesAsync();
            return session.Url;
        }
    }
}
