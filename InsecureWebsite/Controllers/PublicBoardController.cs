using Dapper;
using InsecureWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InsecureWebsite.Controllers;

[Authorize]
public class PublicBoardController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeController> _logger;

    public PublicBoardController(IConfiguration configuration, ILogger<HomeController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        var messages = (await con.QueryAsync<PublicBoardMessageModel>(
            "select [Username], [Message] from [PublicBoard] order by [Id] desc")).ToArray();

        return View(new PublicBoardModel
        {
            Messages = messages
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(PublicBoardMessageModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Message))
        {
            TempData["Error"] = "Must specify Message";
            return RedirectToAction("Index");
        }

        if (model.Message.Length > 2048)
        {
            TempData["Error"] = "Message must be 2048 characters or less in length";
            return RedirectToAction("Index");
        }

        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        await con.OpenAsync();

        await con.ExecuteAsync("insert into [PublicBoard]([Username], [Message]) values (@Username, @Message)",
            new { Username = User.Identity.Name, model.Message });

        TempData["Success"] = "Message successfully posted";

        return RedirectToAction("Index");
    }
}