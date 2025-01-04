using ContentMagican.Database;
using ContentMagican.Models;
using ContentMagican.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using static ContentMagican.Services.UserService;

namespace ContentMagican.Controllers
{
    public class Account : Controller
    {
        UserService _userService;
        ApplicationDbContext _applicationDbContext;

        public Account(UserService userService, ApplicationDbContext applicationDbContext)
        {
            _userService = userService;
            _applicationDbContext = applicationDbContext;
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


            return View("Login", new LoginViewModel() { LoginError = result.ToString().Replace("_", " ") });
        }

        [HttpPost]
        public async Task<IActionResult> RegisterAccount(RegisterViewModel registerModel)
        {
            var result = await _userService.RegisterAccount(registerModel, Url.Action("ConfirmRegisterAccount","Account",null,Request.Scheme));

            if (result == UserService.RegisterCodes.Ok)
            {
                return View("Login", new LoginViewModel() { LoginError = UserService.RegisterCodes.An_Confirmation_Email_Has_Been_Sent_Confirm_Your_Email_To_Continue.ToString().Replace("_", " ") });
            }

            return View("Register", new RegisterViewModel() { Error = result.ToString().Replace("_", " ") });
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmRegisterAccount(string attemptId, string encryptedAttemptId)
        {
            try
            {
                //var urlDecodedEncryptedAtemptId = HttpUtility.UrlDecode(encryptedAtemptId);
                //var urlDecodedAttemptId = HttpUtility.UrlDecode(attemptId);

                var decrypted = CryptService.Decrypt(encryptedAttemptId, _userService.emailConfirmationCryptKey);
                var parameters = decrypted.Split(',');

                var result = _applicationDbContext.RegisterAtempt.Where(a => a.AttemptId == attemptId && encryptedAttemptId == a.EncryptedIdentifier).FirstOrDefault();

                if (result == default)
                {
                    return BadRequest("Confirm fail, please try registering again");
                }

                _applicationDbContext.RegisterAtempt.Remove(result);
                await _applicationDbContext.SaveChangesAsync();



                await _applicationDbContext.Users.AddAsync(new User
                {
                    Email = parameters[0],
                    Username = parameters[1],
                    Password = parameters[2],
                });

                await _applicationDbContext.SaveChangesAsync();

                return View("Login", new LoginViewModel() { LoginError = UserService.LoginCodes.Your_Email_Has_Been_Confirmed_Please_Login.ToString().Replace('_', ' ') });
            }
            catch(Exception e)
            {
                return BadRequest("Confirm fail, please try registering again err:"+e.Message+" " + e.InnerException);
            }
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
