using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        public IActionResult AddOpeningStock()
        {
            return View();
        }
    }
}
