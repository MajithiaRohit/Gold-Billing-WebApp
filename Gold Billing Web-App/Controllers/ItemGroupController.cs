using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class ItemGroupController : Controller
    {
        public IActionResult ItemGroupList()
        {
            return View();
        }
        public IActionResult AddEditItemGroup()
        {
            return View();
        }

    }
}
