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

        public async Task<string> StripeSession(long userId, string productId, string redirectUrl, HttpContext ctx)
        {
            var productService = new ProductService();
            var product = await productService.GetAsync(productId);

            var userInfo = await _userService.RetrieveUserInformation(ctx);

            // Retrieve the user from your database to access StripeCustomerId
            var user = await _applicationDbContext.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            string stripeCustomerId;

            if (string.IsNullOrEmpty(user.CustomerId))
            {
                // Create a new Stripe Customer
                var customerOptions = new Stripe.CustomerCreateOptions
                {
                    Email = userInfo.Email,
                    Metadata = new Dictionary<string, string>
            {
                { "UserId", userInfo.Id.ToString() }
            }
                };

                var customerService = new Stripe.CustomerService();
                var customer = await customerService.CreateAsync(customerOptions);
                stripeCustomerId = customer.Id;

                // Store the Stripe Customer ID in your database
                user.CustomerId = stripeCustomerId;
                _applicationDbContext.Users.Update(user);
                await _applicationDbContext.SaveChangesAsync();
            }
            else
            {
                // Use the existing Stripe Customer ID
                stripeCustomerId = user.CustomerId;
            }

            var sessionOptions = new Stripe.Checkout.SessionCreateOptions
            {
                CancelUrl = redirectUrl,
                SuccessUrl = redirectUrl,
                Customer = stripeCustomerId, // Associate the session with the existing customer
                Metadata = new Dictionary<string, string>
        {
            { "UserId", userInfo.Id.ToString() },
            { "ProductId", productId },
        },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
        {
            new Stripe.Checkout.SessionLineItemOptions
            {
                Price = product.DefaultPriceId,
                Quantity = 1,
            },
        },
                Mode = "payment",
                // Optionally, add other session options like payment methods, billing address collection, etc.
            };

            var sessionService = new Stripe.Checkout.SessionService();
            var session = await sessionService.CreateAsync(sessionOptions);

            // Log the order
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
