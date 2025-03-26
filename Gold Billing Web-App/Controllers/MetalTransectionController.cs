using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gold_Billing_Web_App.Controllers
{
    public class MetalTransectionController : Controller
    {
        private readonly AppDbContext _context;

        public MetalTransectionController(AppDbContext context)
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

        private List<ItemDropDownModel> SetItemDropDown()
        {
            var allowedItems = new List<string> { "Fine Metal", "Cadbury", "Dhal" };
            return _context.Items
                .Where(i => allowedItems.Contains(i.Name!))
                .Select(i => new ItemDropDownModel
                {
                    Id = i.Id!.Value,
                    ItemName = i.Name!
                })
                .ToList();
        }

        [HttpGet]
        public IActionResult GetNextBillNo(string type)
        {
            if (!new[] { "Payment", "Receipt" }.Contains(type))
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
            var lastBill = _context.MetalTransactions
                .Where(t => t.BillNo!.StartsWith(prefix))
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

        public IActionResult GenrateMetalTransectionVoucher(string type = "Payment", int? accountId = null, string? billNo = null)
        {
            if (!new[] { "Payment", "Receipt" }.Contains(type))
            {
                return NotFound();
            }

            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new MetalTransactionViewModel();

            if (!string.IsNullOrEmpty(billNo))
            {
                var transactions = _context.MetalTransactions
                    .Where(t => t.BillNo == billNo)
                    .Include(t => t.Account)
                    .Include(t => t.Item)
                    .ToList();

                if (!transactions.Any())
                {
                    return NotFound();
                }

                model.BillNo = billNo;
                model.Date = transactions.First().Date;
                model.Narration = transactions.First().Narration;
                model.Type = type;
                model.Items = transactions;
                if (model.Items.Any() && model.Items[0].AccountId.HasValue)
                {
                    ViewBag.SelectedAccountId = model.Items[0].AccountId;
                }
            }
            else
            {
                model.BillNo = GenerateSequentialBillNo(type);
                model.Date = DateTime.Now;
                model.Type = type;
                model.Items = new List<MetalTransactionModel> { new MetalTransactionModel { Type = type } };
                if (accountId.HasValue)
                {
                    ViewBag.SelectedAccountId = accountId;
                    model.Items[0].AccountId = accountId;
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
        public async Task<IActionResult> AddMetalTransaction(MetalTransactionViewModel model, int? SelectedAccountId)
        {
            ViewBag.AccountDropDown = SetAccountDropDown();
            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.SelectedAccountId = SelectedAccountId;

            model.Type ??= Request.Form["Type"].FirstOrDefault() ?? "Payment";

            if (!SelectedAccountId.HasValue)
            {
                ModelState.AddModelError("SelectedAccountId", "Account is required.");
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
            }
            else
            {
                foreach (var item in model.Items)
                {
                    if (!item.ItemId.HasValue)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Item is required.");
                    }
                    if (!item.GrossWeight.HasValue || item.GrossWeight <= 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].GrossWeight", "Gross Weight is required.");
                    }
                    if (!item.Tunch.HasValue || item.Tunch <= 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                decimal totalFine = 0;
                var existingTransactions = _context.MetalTransactions.Where(t => t.BillNo == model.BillNo);
                if (existingTransactions.Any())
                {
                    _context.MetalTransactions.RemoveRange(existingTransactions);
                }

                foreach (var item in model.Items!)
                {
                    item.AccountId = SelectedAccountId;
                    item.Type = model.Type;
                    item.BillNo = model.BillNo;
                    item.Date = model.Date;
                    item.Narration = model.Narration;
                    item.Fine = (item.GrossWeight ?? 0) * (item.Tunch ?? 0) / 100;
                    totalFine += item.Fine ?? 0;
                    _context.MetalTransactions.Add(item);
                }

                var account = await _context.Accounts
                    .Include(a => a.GroupAccount)
                    .FirstOrDefaultAsync(a => a.AccountId == SelectedAccountId);

                if (account != null)
                {
                    decimal fineAdjustment = 0;
                    if (account.GroupAccount?.GroupName == "Supplier")
                    {
                        fineAdjustment = model.Type == "Payment" ? -totalFine : totalFine;
                    }
                    else if (account.GroupAccount?.GroupName == "Customer")
                    {
                        fineAdjustment = model.Type == "Receipt" ? -totalFine : totalFine;
                    }
                    account.Fine += fineAdjustment;
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