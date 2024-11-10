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
                            context.Response.Cookies.Append(i.ToString(),Convert.ToBase64String(Encoding.UTF8.GetBytes(principal.Claims.ElementAt(i).Value)));
                        }
                    }
                }

                if (hasValidToken)
                {
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
                    if (context.Request.Path.StartsWithSegments("/Account") || context.Request.Path.StartsWithSegments("/public-endpoint"))
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


