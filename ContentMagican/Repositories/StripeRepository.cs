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
            var list = productService.List().ToList();
            return list;
        }

        public async Task<Customer> GetCustomer(string id)
        {
            CustomerService customerService = new CustomerService();
            return await customerService.GetAsync(id);

        }

        public async Task<Customer> GetCustomerByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email must be provided.", nameof(email));
            }

            var customerService = new CustomerService();
            try
            {
                var listOptions = new CustomerListOptions
                {
                    Email = email,
                    Limit = 1,
                };

                var customers = await customerService.ListAsync(listOptions);
                return customers.Data.FirstOrDefault();
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"Stripe Error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }


    }
}
