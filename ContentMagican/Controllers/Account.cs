using ContentMagican.Database;
using ContentMagican.DTOs;
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
        EmailService _emailService;
        TaskService _taskService;

        public Account(UserService userService, ApplicationDbContext applicationDbContext, EmailService emailService, TaskService taskService)
        {
            _userService = userService;
            _applicationDbContext = applicationDbContext;
            _emailService = emailService;
            _taskService = taskService;
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
            var result = await _userService.RegisterAccount(registerModel, Url.Action("ConfirmRegisterAccount", "Account", null, Request.Scheme));

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
            catch (Exception e)
            {
                return BadRequest("Confirm fail, please try registering again err:" + e.Message + " " + e.InnerException);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
           

            // TODO: Implement your password reset logic here.
            // For example, check if the email exists, generate a reset token, and send an email.

            bool emailExists = _applicationDbContext.Users.Any(a => a.Email == model.Email);
            if (!emailExists)
            {
                model.Error = "No account found with that email address.";
                return View(model);
            }
            var guid = Guid.NewGuid().ToString().Replace("-","");
            var user = _applicationDbContext.Users.Where(a=>a.Email == model.Email).FirstOrDefault();
            string url = $"{Url.Action("ResetPassword", "Account", null, Request.Scheme)}?identifier={HttpUtility.UrlEncode(guid)}";
            _applicationDbContext.Add(new ResetPasswordAttempt() { Identifier = guid, UserId = user.Id });
            await _applicationDbContext.SaveChangesAsync();
            _emailService.SendEmail(model.Email, "Reset Your Password", $"Click the link and folow the instructions inorder to reset your password:\n {url}");

            model.Error = "An email has been sent, follow its instruction inorder to reset your password.";

            return View(model);
        }

        public async Task<IActionResult> ResetPassword(string identifier)
        {
            var result = _applicationDbContext.ResetPasswordAttempt.Where(a => a.Identifier == identifier).FirstOrDefault();
            if(result == default)
            {
                return BadRequest("Invalid reset password attempt");
            }

            return View(new ResetPasswordViewModel() { Identifier = identifier });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel reset)
        {
            var result = _applicationDbContext.ResetPasswordAttempt.Where(a => a.Identifier == reset.Identifier).FirstOrDefault();
            var user = _applicationDbContext.Users.Where(a => a.Id == result.UserId).FirstOrDefault();
            user.Password = BCrypt.Net.BCrypt.HashPassword(reset.NewPassword);
            await _applicationDbContext.SaveChangesAsync();
            _applicationDbContext.ResetPasswordAttempt.Remove(result);
            await _applicationDbContext.SaveChangesAsync();
            return View("Login", new LoginViewModel() { LoginError = "Your password has been reset successfully. Login to access your account!" });
        }

        public async Task<IActionResult> Logout()
        {
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            return RedirectToAction("Main", "Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> RetrieveDueContentCreation([FromBody] KeyDto keyDto)
        {
            if(keyDto.Key != "jsNm7x9L#c2x43ezvrtfgsyuhsydehvjndsjhgxycgdshj24343243rt43t4")
            {
                return BadRequest();
            }

            var tasks =await _taskService.GetAllActiveTasks();
            tasks.RemoveAll(a => a.LastAssessed.DayOfYear < DateTime.Now.DayOfYear);
            var videoAutomations = _applicationDbContext.VideoAutomation.Where(a => tasks.Select(b => b.Id).Contains(a.TaskId)).ToList();
            var socialMediaAccessSessions = _applicationDbContext.SocialMediaAccessSessions.Where(a => tasks.Select(b => b.SocialMediaAccessSessionsId).Contains(a.id)).ToList();   
            var contentCreations = tasks.Select(a => new ContentCreationDto()
            {
                _Task = a,
                VideoAutomation = videoAutomations.Where(b => b.TaskId == a.Id).FirstOrDefault(),
                SocialMediaAccessSession = socialMediaAccessSessions.Where(b => b.id == a.SocialMediaAccessSessionsId).FirstOrDefault()
            }).ToArray();

            foreach (var item in contentCreations)
            {
                item._Task.LastAssessed = DateTime.Now;
                _applicationDbContext.Update(item._Task);
                Console.WriteLine("Assessed Task"+item._Task.LastAssessed);
            }

            await _applicationDbContext.SaveChangesAsync();


            return new JsonResult(contentCreations);
        }
    }
}
