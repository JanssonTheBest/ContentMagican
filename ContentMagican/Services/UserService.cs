using ContentMagican.Database;
using ContentMagican.DTOs;
using ContentMagican.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace ContentMagican.Services
{
    public class UserService
    {
        private readonly IConfiguration _configuration;
        ApplicationDbContext _applicationDbContext;
        private readonly TokenValidationParameters _tokenValidationParameters;


        public UserService(ApplicationDbContext applicationDbContext, IConfiguration configuration, TokenValidationParameters tokenValidationParameters)
        {
            _applicationDbContext = applicationDbContext;
            _configuration = configuration;
            _tokenValidationParameters = tokenValidationParameters;
        }
        public enum RegisterCodes
        {
            Ok,
            An_User_With_That_Email_Already_Exist,
            Email_Is_Not_Valid,
            Passwords_Does_Not_Match,
        }

        public async Task<RegisterCodes> RegisterAccount(RegisterViewModel registerModel)
        {
            string emailPattern = @"^[\w\.-]+@[a-zA-Z\d\.-]+\.[a-zA-Z]{2,}$";


            if (string.IsNullOrEmpty(registerModel.Email))
            {
                return RegisterCodes.Email_Is_Not_Valid;
            }

            if (!Regex.IsMatch(registerModel.Email, emailPattern))
            {
                return RegisterCodes.Email_Is_Not_Valid;
            }

            if (_applicationDbContext.Users.Select(a => a.Email.Equals(registerModel.Email)).FirstOrDefault() != default)
            {
                return RegisterCodes.An_User_With_That_Email_Already_Exist;
            }

            if (!registerModel.Password.Equals(registerModel.ConfirmPassword))
            {
                return RegisterCodes.Passwords_Does_Not_Match;
            }

            await _applicationDbContext.Users.AddAsync(new User
            {
                Email = registerModel.Email,
                Username = registerModel.Username,
                Password = registerModel.Password,
            });

            await _applicationDbContext.SaveChangesAsync();

            return RegisterCodes.Ok;
        }



        public enum LoginCodes
        {
            Ok,
            An_User_With_That_Email_Does_Not_Exist,
            Wrong_Passwords,
        }
        public async Task<LoginCodes> LoginAccount(LoginViewModel loginModel)
        {
            User user = _applicationDbContext.Users.Where(a => a.Email.Equals(loginModel.Email)).FirstOrDefault();
            if (user == default)
            {
                return LoginCodes.An_User_With_That_Email_Does_Not_Exist;
            }

            if (user.Password != loginModel.Password)
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


        public async Task<IEnumerable<SocialMediaAccessSession>> RetrieveUserSocialMediaAccessSessions(HttpContext ctx)
        {
            var user = await RetrieveUserInformation(ctx);
            return _applicationDbContext.SocialMediaAccessSessions.Where(a => a.userId == user.Id);
        }


    }
}
