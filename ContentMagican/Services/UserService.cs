using BCrypt.Net;
using ContentMagican.Database;
using ContentMagican.DTOs;
using ContentMagican.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace ContentMagican.Services
{
    public class UserService
    {
        private readonly IConfiguration _configuration;
        ApplicationDbContext _applicationDbContext;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly EmailService _emailService;
        public string emailConfirmationCryptKey;

        public UserService(ApplicationDbContext applicationDbContext, IConfiguration configuration, TokenValidationParameters tokenValidationParameters, EmailService emailService)
        {
            _applicationDbContext = applicationDbContext;
            _configuration = configuration;
            _tokenValidationParameters = tokenValidationParameters;
            _emailService = emailService;
            emailConfirmationCryptKey = _configuration.GetSection("Cryption")["EmailConfirmationKey"];
        }
        public enum RegisterCodes
        {
            Ok,
            Email_Is_Not_Valid,
            An_User_With_That_Email_Already_Exist,
            Passwords_Do_Not_Match,
            Password_Length_Too_Short,
            Password_Missing_Uppercase,
            Password_Missing_Lowercase,
            Password_Missing_Digit,
            Password_Missing_SpecialCharacter,
            An_Confirmation_Email_Has_Been_Sent_Confirm_Your_Email_To_Continue
        }


        public async Task<RegisterCodes> RegisterAccount(RegisterViewModel registerModel, string confirmationUri)
        {
            // Email validation pattern
            string emailPattern = @"^[\w\.-]+@[a-zA-Z\d\.-]+\.[a-zA-Z]{2,}$";

            // Validate Email Presence
            if (string.IsNullOrEmpty(registerModel.Email))
            {
                return RegisterCodes.Email_Is_Not_Valid;
            }

            // Validate Email Format
            if (!Regex.IsMatch(registerModel.Email, emailPattern))
            {
                return RegisterCodes.Email_Is_Not_Valid;
            }

            // Check if Email Already Exists (Case-Insensitive)
            // Revised line: Removed StringComparison.OrdinalIgnoreCase
            if (_applicationDbContext.Users.Any(a => a.Email == registerModel.Email))
            {
                return RegisterCodes.An_User_With_That_Email_Already_Exist;
            }

            // Check if Passwords Match
            if (!registerModel.Password.Equals(registerModel.ConfirmPassword))
            {
                return RegisterCodes.Passwords_Do_Not_Match;
            }

            // Password Validation

            // 1. Minimum Length Check
            if (registerModel.Password.Length < 8)
            {
                return RegisterCodes.Password_Length_Too_Short;
            }

            if (!Regex.IsMatch(registerModel.Password, @"\d"))
            {
                return RegisterCodes.Password_Missing_Digit;
            }

      
            string attemptId = Guid.NewGuid().ToString().Replace('-', ' ');

            string encryptedId = CryptService.Encrypt(
                $"{registerModel.Email},{registerModel.Username},{BCrypt.Net.BCrypt.HashPassword(registerModel.Password)}",
                emailConfirmationCryptKey
            );

            string confirmationLink = $"{confirmationUri}?attemptId={HttpUtility.UrlEncode(attemptId)}&encryptedAttemptId={HttpUtility.UrlEncode(encryptedId)}";
            _emailService.SendEmail(
                registerModel.Email,
                "Confirm your email address",
                $"Click on the link below to confirm your email address: {confirmationLink}"
            );

            // Save Registration Attempt to Database
            _applicationDbContext.RegisterAtempt.Add(new RegisterAtempt
            {
                EncryptedIdentifier = encryptedId,
                AttemptId = attemptId
            });

            // Save Changes Asynchronously
            await _applicationDbContext.SaveChangesAsync();

            return RegisterCodes.Ok;
        }



        public enum LoginCodes
        {
            Ok,
            An_User_With_That_Email_Does_Not_Exist,
            Wrong_Passwords,
            Your_Email_Has_Been_Confirmed_Please_Login,
        }
        public async Task<LoginCodes> LoginAccount(LoginViewModel loginModel)
        {
            User user = _applicationDbContext.Users.Where(a => a.Email.Equals(loginModel.Email)).FirstOrDefault();
            if (user == default)
            {
                return LoginCodes.An_User_With_That_Email_Does_Not_Exist;
            }

            if (!BCrypt.Net.BCrypt.Verify(loginModel.Password, user.Password))
            {
                return LoginCodes.Wrong_Passwords;
            }
            return LoginCodes.Ok;
        }



        public bool ValidateJwtToken(string token, out ClaimsPrincipal principal)
        {
            principal = null;
            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                SecurityToken validatedToken;
                principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out validatedToken);

                return true;
            }
            catch (SecurityTokenException ex)
            {
                Console.WriteLine($"Token validation failed: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GenerateJwtToken(string email)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);

        }

        public async Task<User> RetrieveUserInformation(HttpContext ctx)
        {
            if (ctx != null)
            {
                if (ctx.Request.Cookies.TryGetValue("jwtToken", out var token))
                {
                    if (ValidateJwtToken(token, out var principal))
                    {
                        string email = principal.Claims.ElementAt(0).Value;
                        var user = _applicationDbContext.Users.Where(a => a.Email.Equals(email)).FirstOrDefault();

                        if (user != default)
                        {
                            return user;
                        }
                    }
                }
            }

            return new User()
            {
                Username = ""
            };
        }


        public async Task<IEnumerable<SocialMediaAccessSession>> RetrieveActiveUserSocialMediaAccessSessions(HttpContext ctx)
        {
            var user = await RetrieveUserInformation(ctx);
            return _applicationDbContext.SocialMediaAccessSessions.Where(a => a.userId == user.Id && a.status == 0);
        }

        public async Task<int> RemoveSocialMediaAccessSession(int socialMediaAccessSessionId, HttpContext ctx)
        {
            var user = await RetrieveUserInformation(ctx);
            var sessions = await RetrieveActiveUserSocialMediaAccessSessions(ctx);

            // Materialize sessions if not already a list
            var sessionList = sessions as List<SocialMediaAccessSession> ?? sessions.ToList();

            foreach (var item in sessionList)
            {
                var tasks = _applicationDbContext.Task
                    .Where(a => a.SocialMediaAccessSessionsId == item.id && a.Status == (int)TaskService.TaskStatus.active)
                    .ToList();

                if (tasks.Any())
                {
                    foreach (var task in tasks)
                    {
                        task.Status = (int)TaskService.TaskStatus.deleted;
                    }
                }
                item.status = 1;
            }

            await _applicationDbContext.SaveChangesAsync();
            return socialMediaAccessSessionId;
        }


    }
}
