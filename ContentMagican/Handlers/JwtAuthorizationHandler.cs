//using ContentMagican.Services;
//using Microsoft.AspNetCore.Http.HttpResults;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore.Metadata.Internal;

//namespace ContentMagican.Handlers
//{
//    public class JwtAuthorizationHandler : Controller
//    {
//        private readonly IHttpContextAccessor _httpContextAccessor;

//        public JwtAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
//        {
//            _httpContextAccessor = httpContextAccessor;
//        }

//        [Route("/Validate")]
//        public async Task<bool> ValidationView()
//        {
//            var context = _httpContextAccessor.HttpContext;
//            if(context.Request.Cookies.TryGetValue("jwtToken", out var token))
//            {
//                if (_userService.ValidateJwtToken(token, out _))
//                {
//                    Console.WriteLine("Succesfully valid");
//                    return true;
//                }
//                Console.WriteLine("unauthrized");

//                return false;
//            }
//            Console.WriteLine("unauthrized");

//            return false;
//        }
//    }
//}
