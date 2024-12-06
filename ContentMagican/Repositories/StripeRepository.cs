using Stripe;
namespace ContentMagican.Repositories
{
    public class StripeRepository
    {
        IConfigurationSection _config;
        public StripeRepository(IConfiguration configuration)
        {
            _config = configuration.GetSection("StripeCredentials");
            string apikey = configuration.GetSection("StripeCredentials")["sk"];
            StripeConfiguration.ApiKey = apikey;
        }

        public async Task BuySubscription()
        {
            PaymentIntent paymentIntent = new PaymentIntent();
            //paymentIntent.
        }

        public async Task<string> GetCheckoutWebhookSecret()
        {
            return _config["checkoutws"];
        }

        public async Task<List<Product>> GetAllProducts()
        {
            ProductService productService = new ProductService();
            var list =  productService.List().ToList();
            return list;
        }

    }
}
