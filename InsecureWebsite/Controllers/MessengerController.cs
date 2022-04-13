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

    public MessengerController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> GetMessages(MessengerGetMessagesFromOtherUserModel model)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DatabaseConnectionString"));
        con.Open();

        var messages = new Dictionary<string, List<string>>();

        try
        {
            
            var reader = await con.ExecuteReaderAsync(
                "select [FromUsername], [ToUsername], [Message] from [UserToUserMessage]" +
                "where ([FromUsername] = '" + model.OtherUser + "' and [ToUsername] = '" + User.Identity.Name + "')" +
                "or ([FromUsername] = '" + User.Identity.Name + "' and [ToUsername] = '" + model.OtherUser + "')");
            while (await reader.ReadAsync())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var fieldName = reader.GetName(i);
                    var fieldValue = reader.GetValue(i).ToString() ?? "NULL";

                    if (messages.TryGetValue(fieldName, out var list))
                    {
                        list.Add(fieldValue);
                    }
                    else
                    {
                        messages[fieldName] = new List<string>(new[] {fieldValue});
                    }
                }
            }

            ViewBag.Success = $"Query successfully ran with OtherUser \"{model.OtherUser}\"";
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Error occurred running database query with OtherUser \"{model.OtherUser}\": {ex.Message}";
        }

        return View("Index", new MessengerModel
        {
            Messages = messages.ToDictionary(x => x.Key, x => x.Value.ToArray())
        });
    }
}