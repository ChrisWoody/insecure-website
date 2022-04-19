using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsecureWebsite.Controllers;

[Authorize]
public class AdminController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity.Name != "Admin")
            return new UnauthorizedResult();

        return View();
    }
}