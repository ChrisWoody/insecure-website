﻿using Dapper;
using InsecureWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InsecureWebsite.Controllers;

[Authorize]
public class MessengerController : Controller
{
    private readonly IConfiguration _configuration;

    public MessengerController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View();
    }

    // Intentionally setup for sql injection by not sanitizing or paramaterizing the input
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetMessages(MessengerGetMessagesFromOtherUserModel model)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        con.Open();

        var messages = Array.Empty<MessengerModel.MessageModel>();

        try
        {
            messages = (await con.QueryAsync<MessengerModel.MessageModel>(
                "select [FromUsername], [ToUsername], [Message] from [UserToUserMessage]" +
                "where ([FromUsername] = '" + model.OtherUser + "' and [ToUsername] = '" + User.Identity.Name + "')" +
                "or ([FromUsername] = '" + User.Identity.Name + "' and [ToUsername] = '" + model.OtherUser + "')"))
                .ToArray();

            ViewBag.Success = $"Query successfully ran with OtherUser \"{model.OtherUser}\"";
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Error occurred running database query with OtherUser \"{model.OtherUser}\": {ex.Message}";
        }

        return View("Index", new MessengerModel
        {
            Messages = messages
        });
    }
}