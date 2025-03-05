using Gold_Billing_Web_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;

namespace Gold_Billing_Web_App.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _dbContext;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, AppDbContext dbContext)
        {
            _logger = logger;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();
            string connectionString = _configuration.GetConnectionString("ConnectionString");
            _logger.LogInformation($"Connection String: {connectionString}");

            try
            {
                // Log all TransactionTypes and row count
                var transactionTypes = await _dbContext.Transactions
                    .Select(t => t.TransactionType)
                    .Distinct()
                    .ToListAsync();
                var transactionCount = await _dbContext.Transactions.CountAsync();
                _logger.LogInformation($"Transaction Count: {transactionCount}, Types: {string.Join(", ", transactionTypes)}");

                // Log all transactions with weights and BillNo
                var allTransactions = await _dbContext.Transactions
                    .Select(t => new { t.BillNo, t.TransactionType, t.Weight, t.NetWt })
                    .ToListAsync();
                foreach (var t in allTransactions)
                {
                    _logger.LogInformation($"Transaction - BillNo: {t.BillNo}, Type: {t.TransactionType}, Weight: {t.Weight}, NetWt: {t.NetWt}");
                }

                // Total Sales Gross Weight
                var salesWeight = await _dbContext.Transactions
                    .Where(t => EF.Functions.Like(t.TransactionType, "Sale"))
                    .SumAsync(t => t.Weight ?? 0);
                var saleReturnsWeight = await _dbContext.Transactions
                    .Where(t => EF.Functions.Like(t.TransactionType, "Sale Return"))
                    .SumAsync(t => t.Weight ?? 0);
                model.TotalSales = salesWeight - saleReturnsWeight;
                _logger.LogInformation($"Sales Weight: {salesWeight}, Sale Returns: {saleReturnsWeight}, Total Sales: {model.TotalSales}");

                // Total Purchase Gross Weight
                var purchaseWeight = await _dbContext.Transactions
                    .Where(t => EF.Functions.Like(t.TransactionType, "Purchase"))
                    .SumAsync(t => t.Weight ?? 0);
                var purchaseReturnsWeight = await _dbContext.Transactions
                    .Where(t => EF.Functions.Like(t.TransactionType, "Purchase Return") || EF.Functions.Like(t.TransactionType, "PurchaseReturn"))
                    .SumAsync(t => t.Weight ?? 0);
                model.TotalPurchase = purchaseWeight - purchaseReturnsWeight;
                _logger.LogInformation($"Purchase Weight: {purchaseWeight}, Purchase Returns: {purchaseReturnsWeight}, Total Purchase: {model.TotalPurchase}");

                // Gross Weight of Stock (use NetWt from SP_Stock_SelectAll)
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var command = new SqlCommand("SP_Stock_SelectAll", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    var reader = command.ExecuteReader();
                    var table = new DataTable();
                    table.Load(reader);

                    // Log all stock rows
                    foreach (DataRow row in table.Rows)
                    {
                        _logger.LogInformation($"Stock - BillNo: {row["BillNo"]}, NetWt: {row["NetWt"]}, LastUpdated: {row["LastUpdated"]}");
                    }

                    var totalStockWeight = table.AsEnumerable()
                        .Sum(row => row["NetWt"] != DBNull.Value ? Convert.ToDecimal(row["NetWt"]) : 0);
                    model.GrossWeight = totalStockWeight;
                    _logger.LogInformation($"Total Stock NetWt: {totalStockWeight}, Gross Weight: {model.GrossWeight}");
                }
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