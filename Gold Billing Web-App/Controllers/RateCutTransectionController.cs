using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gold_Billing_Web_App.Controllers
{
    public class RateCutTransectionController : Controller
    {
        private readonly AppDbContext _context;

        public RateCutTransectionController(AppDbContext context)
        {
            _context = context;
        }

        private List<AccountDropDownModel> SetAccountDropDown()
        {
            return _context.Accounts
                .Include(a => a.GroupAccount)
                .Select(a => new AccountDropDownModel
                {
                    Id = a.AccountId!.Value,
                    AccountName = a.AccountName,
                    GroupName = a.GroupAccount != null ? a.GroupAccount.GroupName : ""
                })
                .ToList();
        }

        private string GenerateSequentialBillNo(string type)
        {
            string prefix = type == "GoldPurchaseRate" ? "GPR" : "GSR";
            var lastBill = _context.RateCutTransactions
                .Where(t => t.BillNo.StartsWith(prefix))
                .OrderByDescending(t => t.BillNo)
                .Select(t => t.BillNo)
                .FirstOrDefault();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastBill))
            {
                lastNumber = int.Parse(lastBill.Substring(prefix.Length));
            }
            return $"{prefix}{(lastNumber + 1):D4}";
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string? type)
        {
            if (string.IsNullOrEmpty(type) || !new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
                return BadRequest("Invalid transaction type");
            }

            try
            {
                string billNo = GenerateSequentialBillNo(type);
                return Json(new { success = true, billNo });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"Error generating bill number: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId)
                .Select(a => new { a.Fine, a.Amount, a.Date })
                .FirstOrDefault();

            if (account != null)
            {
                return Json(new
                {
                    fine = account.Fine,
                    amount = account.Amount,
                    lastBalanceDate = account.Date.ToString("yyyy-MM-dd")
                });
            }
            return Json(new { fine = 0.0, amount = 0.0, lastBalanceDate = (string)null });
        }

        public IActionResult GenrateRateCutVoucher(string type = "GoldPurchaseRate", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "GoldPurchaseRate", "GoldSaleRate" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.AccountDropDown = SetAccountDropDown();
            var model = new RateCutTransactionModel
            {
                Type = type,
                Date = DateTime.Now,
                Weight = 0,
                Tunch = 0,
                Rate = 0,
                Amount = 0
            };

            if (!string.IsNullOrEmpty(billNo))
            {
                var transaction = _context.RateCutTransactions
                    .Include(t => t.Account)
                    .FirstOrDefault(t => t.BillNo == billNo);

                if (transaction == null)
                {
                    return NotFound();
                }

                model = transaction;
                ViewBag.SelectedAccountId = model.AccountId;
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.AccountId = accountId.Value;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRateCutTransaction(RateCutTransactionModel model, int? SelectedAccountId)
        {
            ViewBag.AccountDropDown = SetAccountDropDown();

            model.Type ??= Request.Form["Type"].FirstOrDefault() ?? "GoldPurchaseRate";

            if (!SelectedAccountId.HasValue)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
            }
            else
            {
                model.AccountId = SelectedAccountId.Value;
            }

            if (string.IsNullOrEmpty(model.BillNo) || model.BillNo.Length > 40)
            {
                ModelState.AddModelError("BillNo", "Bill Number is required and must not exceed 40 characters.");
            }

            if (model.Weight <= 0)
            {
                ModelState.AddModelError("Weight", "Weight must be greater than 0.");
            }

            if (model.Tunch <= 0 || model.Tunch > 100)
            {
                ModelState.AddModelError("Tunch", "Tunch must be between 0 and 100.");
            }

            if (model.Rate <= 0)
            {
                ModelState.AddModelError("Rate", "Rate must be greater than 0.");
            }

            if (model.Narration?.Length > 1000)
            {
                ModelState.AddModelError("Narration", "Narration must not exceed 1000 characters.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                decimal fineGold = (model.Weight * model.Tunch) / 100;
                model.Amount = fineGold * model.Rate;

                var account = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == model.AccountId);

                if (model.Id > 0)
                {
                    _context.RateCutTransactions.Update(model);
                }
                else
                {
                    _context.RateCutTransactions.Add(model);
                }

                if (account != null)
                {
                    decimal fineAdjustment = fineGold;
                    decimal amountAdjustment = model.Amount;

                    if (account.GroupAccount?.GroupName == "Supplier")
                    {
                        if (model.Type == "GoldPurchaseRate")
                        {
                            fineAdjustment = -fineGold;
                        }
                        else if (model.Type == "GoldSaleRate")
                        {
                            amountAdjustment = -model.Amount;
                        }
                    }
                    else if (account.GroupAccount?.GroupName == "Customer")
                    {
                        if (model.Type == "GoldPurchaseRate")
                        {
                            amountAdjustment = -model.Amount;
                        }
                        else if (model.Type == "GoldSaleRate")
                        {
                            fineAdjustment = -fineGold;
                        }
                    }

                    account.Fine += fineAdjustment;
                    account.Amount += amountAdjustment;
                }

                await _context.SaveChangesAsync();

                string redirectUrl = Url.Action("Index", "Home")!;
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }
    }
}