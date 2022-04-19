using Dapper;
using InsecureWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InsecureWebsite.Controllers;

[Authorize]
public class MessengerController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeController> _logger;

    public MessengerController(IConfiguration configuration, ILogger<HomeController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    // Intentionally setup for sql injection by not sanitizing or paramaterizing the input
    // Also to help iteration on the injection sql, safely handle invalid sql and return the response to the user
    [HttpGet]
    public async Task<IActionResult> Index(string otherUser)
    {
        if (string.IsNullOrWhiteSpace(otherUser))
            return View();

        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        try
        {
            var messages = (await con.QueryAsync<MessengerModel.MessageModel>(
                "select [FromUsername], [ToUsername], [Message] from [UserToUserMessage] " +
                "where ([FromUsername] = '" + otherUser + "' and [ToUsername] = '" + User.Identity.Name + "') " +
                "or ([FromUsername] = '" + User.Identity.Name + "' and [ToUsername] = '" + otherUser + "')"))
                .ToArray();

            TempData["Success"] = $"Successfully pulled messages with OtherUser \"{otherUser}\"";

            return View("Index", new MessengerModel
            {
                Messages = messages
            });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"An unexpected error occurred trying to find messages with OtherUser \"{otherUser}\": {ex.Message}";
            _logger.LogError(ex, $"Error retrieving messages for {otherUser} with {User.Identity.Name}");
            return View();
        }
    }

    // Intentionally no antiforgery check
    [HttpPost]
    public async Task<IActionResult> SendMessage(MessengerSendMessageToOtherUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.OtherUser) ||
            string.IsNullOrWhiteSpace(model.Message))
        {
            TempData["Error"] = "Must specify OtherUser and Message";
            return RedirectToAction("Index");
        }

        if (model.OtherUser.Length > 20)
        {
            TempData["Error"] = "OtherUser must be 20 characters or less in length";
            return RedirectToAction("Index");
        }

        if (model.Message.Length > 2048)
        {
            TempData["Error"] = "Message must be 2048 characters or less in length";
            return RedirectToAction("Index");
        }

        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        if (await UserExists(model.OtherUser))
        {
            await con.ExecuteAsync("insert into [UserToUserMessage]([FromUsername], [ToUsername], [Message]) values (@FromUsername, @ToUsername, @Message)",
                new { FromUsername = User.Identity.Name, ToUsername = model.OtherUser, model.Message });

            TempData["Success"] = $"Message successfully sent to \"{model.OtherUser}\"";

            return RedirectToAction("Index", new {model.OtherUser});
        }

        TempData["Error"] = $"Other user \"{model.OtherUser}\" doesn't exist";

        return RedirectToAction("Index");
    }

    private async Task<bool> UserExists(string username)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        var results = await con.QueryAsync<string>("select top 1 [Username] from [User] where [Username] = @username", new { username });
        return results.Any();
    }
}