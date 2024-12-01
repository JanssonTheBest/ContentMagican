using ContentMagican.Database;
using ContentMagican.Services;
using Stripe;
namespace ContentMagican.Repositories
{
    public class StripeRepository
    {
        UserService _userService;
        public StripeRepository(IConfiguration configuration, UserService userService)
        {
            string apikey = configuration.GetSection("StripeCredentials")["sk"];
            StripeConfiguration.ApiKey = apikey;
            _userService = userService;
        }

        public async Task BuySubscription()
        {
            PaymentIntent paymentIntent = new PaymentIntent();
            //paymentIntent.
        }

        public async Task<List<Product>> GetAllProducts(HttpContext ctx)
        {
            var productService = new ProductService();
            var priceService = new PriceService();
            var paymentLinkService = new PaymentLinkService();
            var productListOptions = new ProductListOptions { Limit = 100 };
            var products = await productService.ListAsync(productListOptions);

            foreach (var product in products.Data)
            {
                if (!string.IsNullOrEmpty(product.Url))
                {
                    continue;
                }

                var priceListOptions = new PriceListOptions
                {
                    Product = product.Id,
                    Limit = 1,
                };
                var prices = await priceService.ListAsync(priceListOptions);

                if (prices.Data.Any())
                {
                    var priceId = prices.Data.First().Id;

                    var paymentLinkOptions = new PaymentLinkCreateOptions
                    {
                        LineItems = new List<PaymentLinkLineItemOptions>
                {
                    new PaymentLinkLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1,
                    },
                },
                    };
                    var paymentLink = await paymentLinkService.CreateAsync(paymentLinkOptions);
                    var user = await _userService.RetrieveUserInformation(ctx);
                    product.Url = paymentLink.Url += $"?client_reference_id={user.Id}";
                }
                else
                {
                    // Handle products without prices (optional)
                }
            }

            return products.Data.ToList();
        }


    }
}
