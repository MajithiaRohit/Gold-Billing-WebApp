using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class AmmountTransectionController : Controller
    {
        public IActionResult GenrateAmmountTransectionVoucher()
        {
            return View();
        }
    }
}
