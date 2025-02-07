using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class RateCutTransectionController : Controller
    {
        public IActionResult GenrateRateCutVoucher()
        {
            return View();
        }
    }
}
