using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace Gold_Billing_Web_App.Controllers
{
    public class LedgerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LedgerController> _logger;

        public LedgerController(AppDbContext context, ILogger<LedgerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                _logger.LogError("User not logged in or user ID not found in session or claims.");
                throw new InvalidOperationException("User not logged in.");
            }

            if (!int.TryParse(userIdString, out int userId))
            {
                _logger.LogError("Failed to parse UserId from session/claims. Value: {UserIdString}", userIdString);
                throw new InvalidOperationException($"Invalid UserId format: {userIdString}");
            }

            return userId;
        }

        // GET: /Ledger/DetailLedger
        public async Task<IActionResult> DetailLedger()
        {
            var userId = GetCurrentUserId();

            // Fetch accounts for the dropdown
            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .Include(a => a.GroupAccount)
                .Select(a => new AccountDropDownModel
                {
                    Id = (int)a.AccountId!,
                    AccountName = a.AccountName,
                    GroupName = a.GroupAccount != null ? a.GroupAccount.GroupName : "",
                })
                .ToListAsync();

            var model = new LedgerViewModel
            {
                UserId = userId,
                Accounts = accounts,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now
            };

            return View(model);
        }

        // POST: /Ledger/GetTransactions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetTransactions(int accountId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Validate inputs
                if (accountId == 0)
                {
                    return Json(new { success = false, error = "Please select an account." });
                }

                if (startDate > endDate)
                {
                    return Json(new { success = false, error = "Start date cannot be greater than end date." });
                }

                // Fetch opening balance (from OpeningStocks before startDate)
                var openingBalance = await _context.OpeningStocks
                    .Where(os => os.UserId == userId && os.Date <= startDate)
                    .GroupBy(os => os.UserId)
                    .Select(g => new LedgerTransactionModel
                    {
                        Date = startDate,
                        Type = "OPENING BALANCE",
                        RefNo = "",
                        Narration = "",
                        Pc = null,
                        GrWt = null,
                        Less = null,
                        NetWt = null,
                        Tunch = null,
                        Wstg = null,
                        Rate = null,
                        GoldFine = g.Sum(os => os.Fine ?? 0),
                        Amount = g.Sum(os => os.Amount ?? 0)
                    })
                    .FirstOrDefaultAsync();

                // Fetch transactions from Transactions table (Sale, Purchase, SaleReturn, PurchaseReturn)
                var rawTransactions = await _context.Transactions
                    .Where(t => t.UserId == userId && t.AccountId == accountId && t.Date >= startDate && t.Date <= endDate)
                    .ToListAsync();

                var transactions = rawTransactions.Select(t => new LedgerTransactionModel
                {
                    Date = t.Date,
                    Type = t.TransactionType switch
                    {
                        "Sale" => "S",
                        "Purchase" => "P",
                        "SaleReturn" => "SR",
                        "PurchaseReturn" => "PR",
                        _ => t.TransactionType
                    },
                    RefNo = t.BillNo,
                    Narration = t.Narration,
                    Pc = t.Pc,
                    GrWt = t.Weight,
                    Less = (double?)t.Less,
                    NetWt = t.NetWt,
                    Tunch = t.Tunch,
                    Wstg = t.Wastage,
                    Rate = t.Rate,
                    GoldFine = t.Fine,
                    Amount = t.Amount
                }).ToList();

                // Fetch amount transactions (Amount Transaction Payment Received)
                var amountTransactions = await _context.AmountTransactions
                    .Where(at => at.UserId == userId && at.AccountId == accountId && at.Date >= startDate && at.Date <= endDate)
                    .Select(at => new LedgerTransactionModel
                    {
                        Date = at.Date,
                        Type = "AT", // Amount Transaction
                        RefNo = at.BillNo,
                        Narration = at.Narration,
                        Pc = null,
                        GrWt = null,
                        Less = null,
                        NetWt = null,
                        Tunch = null,
                        Wstg = null,
                        Rate = null,
                        GoldFine = 0, // Amount transactions don't affect gold fine
                        Amount = at.Type == "Received" ? -at.Amount : at.Amount // Negative for received, positive for paid
                    })
                    .ToListAsync();

                // Fetch metal transactions (Metal Transaction Payment Received)
                var metalTransactions = await _context.MetalTransactions
                    .Where(mt => mt.UserId == userId && mt.AccountId == accountId && mt.Date >= startDate && mt.Date <= endDate)
                    .Select(mt => new LedgerTransactionModel
                    {
                        Date = mt.Date,
                        Type = "MT", // Metal Transaction
                        RefNo = mt.BillNo,
                        Narration = mt.Narration,
                        Pc = null,
                        GrWt = mt.GrossWeight,
                        Less = null,
                        NetWt = mt.GrossWeight, // Assuming GrossWeight is NetWt for simplicity
                        Tunch = mt.Tunch,
                        Wstg = null,
                        Rate = null,
                        GoldFine = mt.Fine,
                        Amount = 0 // Metal transactions typically don't have an amount
                    })
                    .ToListAsync();

                // Fetch rate cut transactions (Rate Cut Transaction)
                var rateCutTransactions = await _context.RateCutTransactions
                    .Where(rct => rct.UserId == userId && rct.AccountId == accountId && rct.Date >= startDate && rct.Date <= endDate)
                    .Select(rct => new LedgerTransactionModel
                    {
                        Date = rct.Date,
                        Type = "RCT", // Rate Cut Transaction
                        RefNo = rct.BillNo,
                        Narration = rct.Narration,
                        Pc = null,
                        GrWt = rct.Weight,
                        Less = null,
                        NetWt = rct.Weight, // Assuming Weight is NetWt for simplicity
                        Tunch = rct.Tunch,
                        Wstg = null,
                        Rate = rct.Rate,
                        GoldFine = 0, // Rate cut transactions don't directly affect gold fine
                        Amount = rct.Amount
                    })
                    .ToListAsync();

                // Combine all transactions
                var allTransactions = new List<LedgerTransactionModel>();
                if (openingBalance != null && (openingBalance.GoldFine != 0 || openingBalance.Amount != 0))
                {
                    allTransactions.Add(openingBalance);
                }
                allTransactions.AddRange(transactions);
                allTransactions.AddRange(amountTransactions);
                allTransactions.AddRange(metalTransactions);
                allTransactions.AddRange(rateCutTransactions);

                // Sort by date
                allTransactions = allTransactions.OrderBy(t => t.Date).ToList();

                // Calculate running balance
                decimal runningGoldFine = 0;
                decimal runningAmount = 0;
                foreach (var transaction in allTransactions)
                {
                    runningGoldFine += transaction.GoldFine ?? 0;
                    runningAmount += transaction.Amount ?? 0;
                }

                return Json(new { success = true, transactions = allTransactions, runningGoldFine, runningAmount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching transactions for AccountId: {AccountId}", accountId);
                return Json(new { success = false, error = $"An error occurred: {ex.Message}" });
            }
        }
    }
}