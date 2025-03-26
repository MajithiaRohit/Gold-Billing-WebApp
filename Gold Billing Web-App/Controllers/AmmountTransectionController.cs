using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gold_Billing_Web_App.Controllers
{
    public class AmmountTransectionController : Controller
    {
        private readonly AppDbContext _context;

        public AmmountTransectionController(AppDbContext context)
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

        private List<PaymentModeDropDownModel> SetPaymentModeDropDown()
        {
            return _context.PaymentModes
                .Select(pm => new PaymentModeDropDownModel
                {
                    Id = pm.Id,
                    ModeName = pm.ModeName
                })
                .ToList();
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string type)
        {
            if (!new[] { "Payment", "Receive" }.Contains(type))
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

        private string GenerateSequentialBillNo(string transactionType)
        {
            string prefix = transactionType == "Payment" ? "PAYM" : "RECV";
            var lastBill = _context.AmountTransactions
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

        public IActionResult GenrateAmmountTransectionVoucher(string type = "Payment", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Payment", "Receive" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.PaymentModeDropDown = SetPaymentModeDropDown();
            var model = new AmountTransactionModel();

            if (!string.IsNullOrEmpty(billNo))
            {
                var transaction = _context.AmountTransactions
                    .Include(t => t.Account)
                    .Include(t => t.PaymentMode)
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
                model.Date = DateTime.Now;
                model.Type = type;
                model.Amount = 0;
                model.PaymentModeId = 0;
                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.AccountId = accountId.Value;
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult GetPreviousBalance(int accountId)
        {
            var account = _context.Accounts
                .Where(a => a.AccountId == accountId)
                .Select(a => new { a.Fine, a.Amount })
                .FirstOrDefault();

            return Json(account != null ? new { fine = account.Fine, amount = account.Amount } : new { fine = 0.0, amount = 0.0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAmountTransaction(AmountTransactionModel model, int? SelectedAccountId)
        {
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.PaymentModeDropDown = SetPaymentModeDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            model.Type ??= Request.Form["Type"].FirstOrDefault() ?? "Payment";

            if (!SelectedAccountId.HasValue)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
            }

            if (string.IsNullOrEmpty(model.BillNo) || model.BillNo.Length > 40)
            {
                ModelState.AddModelError("BillNo", "Bill Number is required and must not exceed 40 characters.");
            }

            if (string.IsNullOrEmpty(model.Type) || model.Type.Length > 20)
            {
                ModelState.AddModelError("Type", "Type is required and must not exceed 20 characters.");
            }

            if (model.PaymentModeId <= 0)
            {
                ModelState.AddModelError("PaymentModeId", "Payment Mode is required.");
            }

            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Amount must be greater than 0.");
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
                model.AccountId = SelectedAccountId!.Value;
                if (model.Id > 0)
                {
                    _context.AmountTransactions.Update(model);
                }
                else
                {
                    _context.AmountTransactions.Add(model);
                }

                var account = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == model.AccountId);

                if (account != null)
                {
                    decimal amountAdjustment = 0;
                    if (account.GroupAccount?.GroupName == "Supplier")
                    {
                        amountAdjustment = model.Type == "Payment" ? -model.Amount : model.Amount;
                    }
                    else if (account.GroupAccount?.GroupName == "Customer")
                    {
                        amountAdjustment = model.Type == "Receive" ? -model.Amount : model.Amount;
                    }
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