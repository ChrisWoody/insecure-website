using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InsecureWebsite.Models;

namespace InsecureWebsite.Controllers;

public class AccountController : Controller
{
    private readonly IConfiguration _configuration;

    public AccountController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult Register()
    {
        if (User.Identity.IsAuthenticated)
            return LocalRedirect("/Profile");

        return View(new LoginRegisterModel());
    }

    // Without antiforgery or captcha, its possible to spam user creation
    [HttpPost]
    public async Task<IActionResult> Register(LoginRegisterModel loginRegisterModel)
    {
        if (string.IsNullOrWhiteSpace(loginRegisterModel.Username) ||
            string.IsNullOrWhiteSpace(loginRegisterModel.Password))
        {
            ViewBag.Error = "Must specify username and password";
            return View();
        }

        if (loginRegisterModel.Username.Length > 20 ||
            loginRegisterModel.Password.Length > 20)
        {
            ViewBag.Error = "Username and password must be less than 20 character";
            return View();
        }

        if (!loginRegisterModel.Password.All(_configuration["AllowedPasswordCharacters"].Contains))
        {
            ViewBag.Error = "Password must only contain the following characters: " + _configuration["AllowedPasswordCharacters"];
            return View();
        }

        if (await UserExists(loginRegisterModel.Username))
        {
            ViewBag.Error = $"A user already exists with the username '{loginRegisterModel.Username}'";
            return View();
        }

        await CreateUser(loginRegisterModel);

        ViewBag.Success = "You're account has been created, you can now login";
        return View("Login");
    }

    private async Task CreateUser(LoginRegisterModel loginRegisterModel)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        con.Open();
        await con.ExecuteAsync("insert into [User] ([Username], [Password]) values (@Username, @Password)", new
        {
            loginRegisterModel.Username,
            Password = CipherStringWithAnUnguessableMechanism(loginRegisterModel.Password)
        });
    }

    private async Task<bool> UserExists(string username)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        con.Open();

        var results = await con.QueryAsync<string>("select top 1 [Username] from [User] where [Username] = @username", new { username });
        return results.Any();
    }

    public IActionResult Login()
    {
        if (User.Identity.IsAuthenticated)
            return LocalRedirect("/Profile");

        return View(new LoginRegisterModel());
    }

    // Without antiforgery or captcha, its possible to spam user logins to guess the passwords
    [HttpPost]
    public async Task<IActionResult> Login(LoginRegisterModel loginRegisterModel)
    {
        if (string.IsNullOrWhiteSpace(loginRegisterModel.Username)
            || string.IsNullOrWhiteSpace(loginRegisterModel.Password))
        {
            ViewBag.Error = "Must specify username and password";
            return View();
        }

        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        con.Open();

        var password = await con.ExecuteScalarAsync<string>("select [Password] from [User] where [Username] = @Username", new
        {
            loginRegisterModel.Username
        });

        if (string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = $"User not found with the username '{loginRegisterModel.Username}'";
            return View();
        }

        var cipheredPassword = CipherStringWithAnUnguessableMechanism(loginRegisterModel.Password);
        if (!string.Equals(password, cipheredPassword, StringComparison.CurrentCultureIgnoreCase))
        {
            ViewBag.Error = $"User '{loginRegisterModel.Username}' found but the password is incorrect";
            return View();
        }

        var claims = new List<Claim>()
        {
            new(ClaimTypes.Name, loginRegisterModel.Username)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties()
            {
                IsPersistent = true
            });

        return LocalRedirect("/Profile");
    }

    // Without antiforgery, its possible to cause user to logout if clicking on a link or XSS
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }

    private string CipherStringWithAnUnguessableMechanism(string str)
    {
        var cipheredChars = str.ToArray().Select((x, i) =>
        {
            var index = _configuration["AllowedPasswordCharacters"].IndexOf(x);
            var newIndex = index + i;
            if (newIndex >= _configuration["AllowedPasswordCharacters"].Length)
            {
                newIndex -= _configuration["AllowedPasswordCharacters"].Length;
            }
            return _configuration["AllowedPasswordCharacters"][newIndex];
        }).ToArray();

        return new string(cipheredChars);
    }
}