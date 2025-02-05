using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemController : Controller
    {
        public IActionResult ItemList()
        {
            return View();
        }
        public IActionResult AddEditItem()
        {
            return View();
        }

    }
}
