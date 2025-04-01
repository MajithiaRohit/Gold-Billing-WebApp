using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using System.Diagnostics;
using System.Threading.Tasks;
using Gold_Billing_Web_App.Models.ViewModels;

namespace Gold_Billing_Web_App.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();

            try
            {
                var salesWeight = await _context.Transactions
                    .Where(t => t.TransactionType == "Sale")
                    .SumAsync(t => t.Weight ?? 0);
                var saleReturnsWeight = await _context.Transactions
                    .Where(t => t.TransactionType == "SaleReturn")
                    .SumAsync(t => t.Weight ?? 0);
                model.TotalSales = salesWeight - saleReturnsWeight;

                var purchaseWeight = await _context.Transactions
                    .Where(t => t.TransactionType == "Purchase")
                    .SumAsync(t => t.Weight ?? 0);
                var purchaseReturnsWeight = await _context.Transactions
                    .Where(t => t.TransactionType == "PurchaseReturn")
                    .SumAsync(t => t.Weight ?? 0);
                model.TotalPurchase = purchaseWeight - purchaseReturnsWeight;

                var totalStockWeight = await _context.OpeningStocks
                    .SumAsync(os => os.NetWt ?? 0);
                model.GrossWeight = totalStockWeight;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction or stock data from database");
                model.TotalSales = 0;
                model.TotalPurchase = 0;
                model.GrossWeight = 0;
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}