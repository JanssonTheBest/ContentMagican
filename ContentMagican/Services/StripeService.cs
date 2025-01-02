using ContentMagican.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Routing;
using Stripe;
using Stripe.BillingPortal;
using Stripe.Checkout;
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


        //public async Task<Product> GetRelevantProductFromUser(HttpContext ctx)
        //{
        //    CustomerService customerService = new CustomerService();
        //    var user = await _userService.RetrieveUserInformation(ctx);

        //    if (string.IsNullOrEmpty(user.CustomerId))
        //    {
        //        return new Product
        //        {
        //            Name = "Free Tier",
        //        };
        //    }

        //    var customer = await customerService.GetAsync(user.CustomerId);
        //    var subscriptionService = new SubscriptionService();
        //    var productService = new ProductService();

        //    // Retrieve all active subscriptions for the customer
        //    var activeSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
        //    {
        //        Customer = customer.Id,
        //        Status = "active",
        //    });

        //    var subscription = activeSubscriptions.FirstOrDefault();

        //    if (subscription == default)
        //    {
        //        return new Product
        //        {
        //            Name = "free tier",
        //        };
        //    }

        //    var subscriptionItem = subscription.Items.Data.FirstOrDefault();
        //    if (subscriptionItem != null)
        //    {
        //        var product = await productService.GetAsync(subscriptionItem.Price.ProductId);
        //        product.Metadata.Add("CancelAtPeriodEnd", Convert.ToString(subscription.CancelAtPeriodEnd));
        //        return product;
        //    }

        //    return new Product
        //    {
        //        Name = "Free Tier",
        //    };
        //}


        public async Task<Product> GetRelevantProductFromUser(HttpContext ctx)
        {
            // 1. Retrieve user data
            CustomerService customerService = new CustomerService();
            var user = await _userService.RetrieveUserInformation(ctx);

            // If no associated customer, return Free Tier
            if (string.IsNullOrEmpty(user.CustomerId))
            {
                return new Product
                {
                    Name = "Free Tier",
                };
            }

            // 2. Retrieve the Stripe customer by CustomerId
            var customer = await customerService.GetAsync(user.CustomerId);
            var subscriptionService = new SubscriptionService();
            var productService = new ProductService();

            // 3. Retrieve all active subscriptions for the customer
            //    (active = not canceled yet, though can still be scheduled to cancel)
            var activeSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
            {
                Customer = customer.Id,
                Status = "active",
            });

            // 4. Among active subscriptions, prioritize the ones NOT scheduled to cancel (CancelAtPeriodEnd == false).
            //    Then, if none exist, pick a subscription that IS scheduled to cancel (CancelAtPeriodEnd == true).
            var subscription = activeSubscriptions
                .Data
                .OrderBy(sub => sub.CancelAtPeriodEnd) // false (0) before true (1)
                .FirstOrDefault();

            // 5. If no active subscriptions at all, return Free Tier
            if (subscription == default)
            {
                return new Product
                {
                    Name = "Free Tier",
                };
            }

            // 6. Otherwise, retrieve the corresponding Product for the chosen subscription
            var subscriptionItem = subscription.Items.Data.FirstOrDefault();
            if (subscriptionItem != null)
            {
                var product = await productService.GetAsync(subscriptionItem.Price.ProductId);

                // Add extra metadata showing if this subscription is set to cancel
                product.Metadata["CancelAtPeriodEnd"] = subscription.CancelAtPeriodEnd.ToString();

                return product;
            }

            // 7. Fallback: If we cannot find the product, default back to Free Tier
            return new Product
            {
                Name = "Free Tier",
            };
        }


        public async Task<string> StripeSession(long userId, string productId, string redirectUrl, HttpContext ctx)
        {
            var productService = new ProductService();
            var product = await productService.GetAsync(productId);

            var userInfo = await _userService.RetrieveUserInformation(ctx);

            var user = await _applicationDbContext.Users.FindAsync((int)userId);
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
                Customer = stripeCustomerId,
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
                Mode = "subscription",
                //PaymentIntentData = new SessionPaymentIntentDataOptions
                //{
                //    SetupFutureUsage = "off_session", 
                //},
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


        public async Task<string> CancelSubscriptionAtPeriodEnd(HttpContext ctx)
        {
            // 1. Get the user
            var user = await _userService.RetrieveUserInformation(ctx);
            if (string.IsNullOrEmpty(user.CustomerId))
            {
                return "No Stripe customer found for this user.";
            }

            // 2. Retrieve the user's active subscription
            var subscriptionService = new SubscriptionService();
            var activeSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
            {
                Customer = user.CustomerId,
                Status = "active",
                Limit = 1
            });

            var subscription = activeSubscriptions.FirstOrDefault();
            if (subscription == null)
            {
                return "No active subscription found to cancel.";
            }

            // 3. Schedule the subscription to cancel at the end of the current billing period
            var updateOptions = new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true
            };

            var updatedSubscription = await subscriptionService.UpdateAsync(subscription.Id, updateOptions);

            // 4. Verify the subscription is set to cancel at period end
            if (updatedSubscription.CancelAtPeriodEnd == true)
            {
                return $"Subscription {subscription.Id} is scheduled to cancel at the end of the current billing period.";
            }

            return "Unable to schedule cancellation. Please try again or contact support.";
        }



    }
}
