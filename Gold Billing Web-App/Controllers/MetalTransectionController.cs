using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class MetalTransectionController : Controller
    {
        public IActionResult GenrateMetalTransectionVoucher()
        {
            return View();
        }
    }
}
