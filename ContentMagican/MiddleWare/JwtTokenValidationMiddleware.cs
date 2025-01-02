using ContentMagican.Services;
using System.Text;

namespace ContentMagican.MiddleWare
{

    public class JwtTokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;

        public JwtTokenValidationMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            //#if DEBUG
            //            if (context.Request.Host.Host.Contains("ngrok") &&
            //               context.Request.Path.Equals("/Tasks/ChangeTaskSettings", StringComparison.OrdinalIgnoreCase))
            //            {
            //                var localhostUrl = $"https://localhost:7088{context.Request.Path}";
            //                context.Response.Redirect(localhostUrl);
            //                return;
            //            }
            //#endif

            using (var scope = _serviceProvider.CreateScope())
            {
                var userService = scope.ServiceProvider.GetRequiredService<UserService>();
                bool hasValidToken = false;

                if (context.Request.Cookies.TryGetValue("jwtToken", out string token))
                {
                    if (userService.ValidateJwtToken(token, out var principal))
                    {
                        hasValidToken = true;
                        context.User = principal;

                        for (int i = 0; i < principal.Claims.Count(); i++)
                        {
                            context.Response.Cookies.Append(i.ToString(), Convert.ToBase64String(Encoding.UTF8.GetBytes(principal.Claims.ElementAt(i).Value)));
                        }
                    }
                }

                if (hasValidToken)
                {

                    if (!context.Request.Path.StartsWithSegments("/Account/Logout"))
                    {
                        try
                        {
                            var user = await userService.RetrieveUserInformation(context);
                            var stripeService = scope.ServiceProvider.GetRequiredService<StripeService>();
                            var subscription = await stripeService.GetRelevantProductFromUser(context);
                            if (!subscription.Active)
                            {
                                // Redirect authenticated users away from /Account paths
                                if (!context.Request.Path.StartsWithSegments("/Plan/Main") && !context.Request.Path.StartsWithSegments("/Subscription"))
                                {
                                    context.Response.Redirect("/Plan/Main");
                                    return;

                                }
                            }
                            else
                            {
                                await _next(context);
                            }
                        }
                        catch (Exception e)
                        {
                            context.Response.Redirect("/Plan/Main");
                            return;
                        }
                    }


                    if (context.Request.Path.StartsWithSegments("/Account") && !context.Request.Path.StartsWithSegments("/Account/Logout"))
                    {
                        // Redirect authenticated users away from /Account paths
                        context.Response.Redirect("/Dashboard/Main");
                        return;
                    }
                    else
                    {
                        // Allow access to other paths
                        await _next(context);
                        return;
                    }
                }
                else
                {
                    if (context.Request.Path.StartsWithSegments("/Account")
                        || context.Request.Path.StartsWithSegments("/Info")
                        || context.Request.Path.StartsWithSegments("/tiktokXsOLE8u4HYO2pcOTRIhcNtrlkkKW6ulr.txt")
                        || context.Request.Path.StartsWithSegments("/Stripewebhook")
                        || context.Request.Path.StartsWithSegments("/Tiktok")
                        )
                    {
                        // Allow unauthenticated users to access /Account and public endpoints
                        await _next(context);
                        return;
                    }
                    else
                    {
                        // Redirect unauthenticated users to login
                        context.Response.Redirect("/Account/Login");
                        return;
                    }
                }
            }
        }

    }
}


