using Stripe;
namespace ContentMagican.Repositories
{
    public class StripeRepository
    {

        public StripeRepository(IConfiguration configuration)
        {
            string apikey = configuration.GetSection("StripeCredentials")["sk"];
            StripeConfiguration.ApiKey = apikey;
        }

        public async Task BuySubscription()
        {
            PaymentIntent paymentIntent = new PaymentIntent();
            //paymentIntent.
        }

        public async Task<List<Product>> GetAllProducts()
        {
            ProductService productService = new ProductService();
            var list =  productService.List().ToList();
            return list;
        }

    }
}
