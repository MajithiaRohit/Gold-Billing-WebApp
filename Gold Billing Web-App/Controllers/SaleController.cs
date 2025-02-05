using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class SaleController : Controller
    {
        public IActionResult GenrateSaleBill()
        {
            return View();
        }
    }
}
