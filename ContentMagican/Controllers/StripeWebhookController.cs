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
                case EventTypes.CustomerSubscriptionDeleted:
                    return await HandleCustomerSubscriptionDeleted(stripeEvent);
            }

            return BadRequest("Didnt match");
        }



        public async Task<IActionResult> HandleCheckout(Event stripeEvent)
        {
            
            Session checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;


            if (checkoutSession == null)
            {
                return BadRequest("Invalid session data.");
            }
            if (checkoutSession.PaymentStatus != "paid")
            {
                return Ok("Payment not completed.");
            }
            var user = await _applicationDbContext.Users
                .FirstOrDefaultAsync(u => u.CustomerId == checkoutSession.CustomerId);

            if (user == null)
            {
                return BadRequest("User not found.");
            }

            try
            {
                var paymentIntentService = new PaymentIntentService();
                var paymentMethodService = new PaymentMethodService();
                var customerService = new CustomerService();
                string paymentIntentId = checkoutSession.PaymentIntentId;

                if (string.IsNullOrEmpty(paymentIntentId))
                {
                    return BadRequest("PaymentIntentId is missing from the session.");
                }
                PaymentIntent paymentIntent = await paymentIntentService.GetAsync(paymentIntentId);

                if (paymentIntent == null)
                {
                    return BadRequest("PaymentIntent not found.");
                }

                string paymentMethodId = paymentIntent.PaymentMethodId;

                if (string.IsNullOrEmpty(paymentMethodId))
                {
                    return BadRequest("PaymentMethodId is missing from the PaymentIntent.");
                }

                var attachOptions = new PaymentMethodAttachOptions
                {
                    Customer = user.CustomerId,
                };
                await paymentMethodService.AttachAsync(paymentMethodId, attachOptions);

                var customerUpdateOptions = new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = paymentMethodId
                    }
                };
                await customerService.UpdateAsync(user.CustomerId, customerUpdateOptions);
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
            return Ok($"Payment successful. Default payment method updated for customer {checkoutSession.CustomerId}.");
        }


        private async Task<IActionResult> HandleCustomerSubscriptionDeleted(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Subscription;
            if (subscription == null)
            {
                return BadRequest("could not cast object");
            }

            
            var customer = await _stripeRepository.GetCustomer(subscription.CustomerId);
            if (customer == null)
            {
                return BadRequest("Customer Not Found");
            }

            var user = _applicationDbContext.Users.FirstOrDefault(u => u.Email == customer.Email);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            user.PlanId = "free"; 
            await _applicationDbContext.SaveChangesAsync();
            return Ok(user);
        }

    }
}
