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

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View("Index", await GetProfileModel());
    }

    // Without antiforgery, its possible to spam XSS or CSRF to change a user's password (and don't even need to know the current one)
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            TempData["Error"] = "Must specify a password";
            return RedirectToAction("Index");
        }

        if (model.Password.Length > 20)
        {
            TempData["Error"] = "Password must be less than 20 characters";
            return RedirectToAction("Index");
        }

        if (!model.Password.All(_configuration["AllowedPasswordCharacters"].Contains))
        {
            TempData["Error"] = "Password must only contain the following characters: " + _configuration["AllowedPasswordCharacters"];
            return RedirectToAction("Index");
        }

        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        await con.ExecuteAsync("update [User] set [Password] = @Password where [Username] = @Username", new
        {
            Username = User.Identity.Name,
            Password = CipherStringWithAnUnguessableMechanism(model.Password)
        });

        TempData["Success"] = "You're password has been updated successfully";
        return RedirectToAction("Index");
    }

    // No sanitization of the input
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostMessage(PostMessageModel model)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();
        await con.ExecuteAsync("insert into [UserMessage]([Username], [DisplayRaw], [Hide], [Message]) values (@Username, 1, 0, @Message)",
            new {Username = User.Identity.Name, model.Message});

        return RedirectToAction("Index");
    }

    // No validation that the person updating the message has access to that message (i.e. is the same user)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRawMessage(ProfileMessageModel model)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();
        await con.ExecuteAsync("update [UserMessage] set [DisplayRaw] = case when [DisplayRaw] = 1 then 0 else 1 end where [Id] = @Id", model);

        return RedirectToAction("Index");
    }

    // No validation that the person deleting the message has access to that message (i.e. is the same user)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(ProfileMessageModel model)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();
        await con.ExecuteAsync("update [UserMessage] set [Hide] = 1 where [Id] = @Id", model);

        return RedirectToAction("Index");
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

    private async Task<ProfileModel> GetProfileModel()
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        var messages = await con.QueryAsync<ProfileMessageModel>(
            "select [Id], [DisplayRaw], [Hide], [Message] from [UserMessage] where [Username] = @Username",
            new { Username = User.Identity.Name });

        var model = new ProfileModel
        {
            Username = User.Identity.Name,
            Messages = messages?.Where(x => !x.Hide).ToArray() ?? Array.Empty<ProfileMessageModel>()
        };
        return model;
    }
}