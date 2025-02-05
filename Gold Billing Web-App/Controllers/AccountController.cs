using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult AccountList()
        {
            return View();
        }
        public IActionResult AddEditAccount()
        {
            return View();
        }
    }
}
