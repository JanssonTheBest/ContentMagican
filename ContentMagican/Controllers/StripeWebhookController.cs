using ContentMagican.Database;
using ContentMagican.Repositories;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                //case EventTypes.CustomerSubscriptionDeleted:
                //    return await HandleCustomerSubscriptionDeleted(stripeEvent);
            }

            return BadRequest("Didnt match");
        }


        public async Task<IActionResult> HandleCheckout(Event stripeEvent)
        {
            if (stripeEvent == null || stripeEvent.Data?.Object == null)
            {
                return BadRequest("Invalid event data.");
            }

            // Extract the session object from the event
            Session checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;

            if (checkoutSession == null)
            {
                return BadRequest("Invalid session data.");
            }

            // Ensure the session payment status is "paid"
            if (checkoutSession.PaymentStatus != "paid")
            {
                return Ok("Payment not completed.");
            }

            // Get the customer ID from the session
            var customerId = checkoutSession.CustomerId;
            if (string.IsNullOrEmpty(customerId))
            {
                return BadRequest("Customer ID is missing in the session.");
            }

            // Look up the user in your database using the customer ID
            var user = await _applicationDbContext.Users
                .FirstOrDefaultAsync(u => u.CustomerId == customerId);

            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // Extract the subscription ID from the session
            var subscriptionId = checkoutSession.SubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return BadRequest("Subscription ID is missing in the session.");
            }

            try
            {
                var subscriptionService = new SubscriptionService();

                // Retrieve all active subscriptions for the customer
                var activeSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
                {
                    Customer = customerId,
                    Status = "active",
                });

                // Cancel all active subscriptions except the current one
                foreach (var activeSubscription in activeSubscriptions.Data)
                {
                    if (activeSubscription.Id != subscriptionId)
                    {
                        await subscriptionService.CancelAsync(activeSubscription.Id, new SubscriptionCancelOptions
                        {
                            InvoiceNow = true, // Finalize any outstanding invoices
                            Prorate = true     // Prorate the cancellation if applicable
                        });
                    }
                }

                // Update the customer's default payment method (if needed)
                var subscription = await subscriptionService.GetAsync(subscriptionId);
                if (subscription == null)
                {
                    return BadRequest("Subscription not found.");
                }

                if (!string.IsNullOrEmpty(subscription.DefaultPaymentMethodId))
                {
                    var customerService = new CustomerService();
                    var customerUpdateOptions = new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = subscription.DefaultPaymentMethodId
                        }
                    };
                    await customerService.UpdateAsync(customerId, customerUpdateOptions);
                }

                return Ok($"Payment successful. Old subscriptions canceled, and new subscription {subscriptionId} updated for customer {customerId}.");
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"Stripe error: {ex.Message}");
                return BadRequest($"Stripe error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest($"Error: {ex.Message}");
            }
        }


        //public async Task<IActionResult> HandleCheckout(Event stripeEvent)
        //{
        //    if (stripeEvent == null || stripeEvent.Data?.Object == null)
        //    {
        //        return BadRequest("Invalid event data.");
        //    }

        //    // Extract the session object from the event
        //    Session checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;

        //    if (checkoutSession == null)
        //    {
        //        return BadRequest("Invalid session data.");
        //    }

        //    // Ensure the session payment status is "paid"
        //    if (checkoutSession.PaymentStatus != "paid")
        //    {
        //        return Ok("Payment not completed.");
        //    }

        //    // Get the customer ID from the session
        //    var customerId = checkoutSession.CustomerId;
        //    if (string.IsNullOrEmpty(customerId))
        //    {
        //        return BadRequest("Customer ID is missing in the session.");
        //    }

        //    // Look up the user in your database using the customer ID
        //    var user = await _applicationDbContext.Users
        //        .FirstOrDefaultAsync(u => u.CustomerId == customerId);

        //    if (user == null)
        //    {
        //        return BadRequest("User not found.");
        //    }

        //    // Extract the subscription ID from the session
        //    var subscriptionId = checkoutSession.SubscriptionId;
        //    if (string.IsNullOrEmpty(subscriptionId))
        //    {
        //        return BadRequest("Subscription ID is missing in the session.");
        //    }

        //    try
        //    {
        //        var subscriptionService = new SubscriptionService();
        //        var customerService = new CustomerService();

        //        // Retrieve the subscription details from Stripe
        //        var subscription = await subscriptionService.GetAsync(subscriptionId);

        //        if (subscription == null)
        //        {
        //            return BadRequest("Subscription not found.");
        //        }

        //        // Check for existing active subscriptions for the customer
        //        var existingSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
        //        {
        //            Customer = customerId,
        //            Status = "active",
        //            Limit = 1,
        //        });

        //        if (existingSubscriptions.Data.Count > 0)
        //        {
        //            var existingSubscription = existingSubscriptions.Data[0];
        //            // Cancel the existing subscription if it's not the same as the new one
        //            if (existingSubscription.Id != subscriptionId)
        //            {
        //                await subscriptionService.CancelAsync(existingSubscription.Id, new SubscriptionCancelOptions
        //                {
        //                    InvoiceNow = true, // Finalize any outstanding invoices
        //                    Prorate = true     // Prorate the cancellation if applicable
        //                });
        //            }
        //        }

        //        // Update the customer's default payment method if available in the subscription
        //        if (!string.IsNullOrEmpty(subscription.DefaultPaymentMethodId))
        //        {
        //            var customerUpdateOptions = new CustomerUpdateOptions
        //            {
        //                InvoiceSettings = new CustomerInvoiceSettingsOptions
        //                {
        //                    DefaultPaymentMethod = subscription.DefaultPaymentMethodId
        //                }
        //            };
        //            await customerService.UpdateAsync(customerId, customerUpdateOptions);
        //        }

        //        return Ok($"Payment successful. Subscription updated for customer {customerId}.");
        //    }
        //    catch (StripeException ex)
        //    {
        //        Console.WriteLine($"Stripe error: {ex.Message}");
        //        return BadRequest($"Stripe error: {ex.Message}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error: {ex.Message}");
        //        return BadRequest($"Error: {ex.Message}");
        //    }
        //}







    }
}
