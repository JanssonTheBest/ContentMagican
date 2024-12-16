using ContentMagican.Database;
using ContentMagican.MiddleWare;
using ContentMagican.Repositories;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);


//Configurate entityframework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddSingleton<FFmpegService>();
builder.Services.AddScoped<StripeRepository>();
builder.Services.AddScoped<StripeService>();
builder.Services.AddScoped<TiktokService>();


// Add services to the container.
builder.Services.AddControllersWithViews();


builder.Services.AddSingleton<OpenAIService>();
builder.Services.AddSingleton<AzureSpeechService>();

var enableBackgroundTaskHandler = Convert.ToBoolean(builder.Configuration.GetSection("GeneralSettings")["EnableBackgroundTaskHandler"]);
if (enableBackgroundTaskHandler)
{
    builder.Services.AddHostedService<TaskHandlerService>();
}


// Load JWT settings from configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");


var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtSettings["Issuer"],
    ValidAudience = jwtSettings["Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]))
};

// Configure JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = tokenValidationParameters;



});

builder.Services.AddSingleton(tokenValidationParameters);



builder.Services.AddAuthorization();



var app = builder.Build();




// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}



app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseMiddleware<JwtTokenValidationMiddleware>();


app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Main}/{id?}");

app.Run();


