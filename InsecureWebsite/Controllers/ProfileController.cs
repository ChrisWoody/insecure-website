using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InsecureWebsite.Models;

namespace InsecureWebsite.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IConfiguration _configuration;

    public ProfileController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View();
    }

    // Without antiforgery, its possible to spam XSS or CSRF to change a user's password (and don't even need to know the current one)
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ViewBag.Error = "Must specify a password";
            return View("Index");
        }

        if (model.Password.Length > 20)
        {
            ViewBag.Error = "Password must be less than 20 characters";
            return View("Index");
        }

        if (!model.Password.All(_configuration["AllowedPasswordCharacters"].Contains))
        {
            ViewBag.Error = "Password must only contain the following characters: " + _configuration["AllowedPasswordCharacters"];
            return View("Index");
        }

        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        con.Open();

        var password = await con.ExecuteAsync("update [User] set [Password] = @Password where [Username] = @Username", new
        {
            Username = User.Identity.Name,
            Password = CipherStringWithAnUnguessableMechanism(model.Password)
        });

        ViewBag.Success = "You're password has been updated successfully";
        return View("Index");
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