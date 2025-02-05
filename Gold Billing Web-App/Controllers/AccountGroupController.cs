using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class AccountGroupController : Controller
    {
        public IActionResult AccountGroupList()
        {
            return View();
        }
        public IActionResult AddEditAccountGroup()
        {
            return View();
        }
    }
}
