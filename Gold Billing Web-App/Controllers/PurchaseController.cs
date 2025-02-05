using Microsoft.AspNetCore.Mvc;

namespace Gold_Billing_Web_App.Controllers
{
    public class PurchaseController : Controller
    {
        public IActionResult GenratePurchaseBill()
        {
            return View();
        }
    }
}
