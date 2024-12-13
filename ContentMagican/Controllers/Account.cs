using ContentMagican.Models;
using ContentMagican.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static ContentMagican.Services.UserService;

namespace ContentMagican.Controllers
{
    public class Account : Controller
    {
        UserService _userService;

        public Account(UserService userService)
        {
            _userService = userService;
        }

        public async Task<IActionResult> Login()
        { 
            return View(new LoginViewModel());
        }


        public IActionResult Register()
        {
        

            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> LoginAccount(LoginViewModel loginModel)
        {
            var result = await _userService.LoginAccount(loginModel);

            if (result == UserService.LoginCodes.Ok)
            {


                string jwtToken = await _userService.GenerateJwtToken(loginModel.Email);


                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(7),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                };

                Response.Cookies.Append("jwtToken", jwtToken);
                return RedirectToAction("Main", "Dashboard");
            }


            return View("Login", new LoginViewModel() { Error = result.ToString().Replace("_", " ") });
        }

        [HttpPost]
        public async Task<IActionResult> RegisterAccount(RegisterViewModel registerModel)
        {
            var result = await _userService.RegisterAccount(registerModel);

            if (result == UserService.RegisterCodes.Ok)
            {
                return View("Login");
            }

            return View("Register", new RegisterViewModel() { Error = result.ToString().Replace("_", " ") });
        }



        public async Task<IActionResult> Logout()
        {
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            return RedirectToAction("Main", "Dashboard");
        }
    }
}
