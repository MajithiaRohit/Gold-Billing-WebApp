using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Models.ViewModels;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Gold_Billing_Web_App.Controllers
{
    public class OpeningStockController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OpeningStockController> _logger;

        public OpeningStockController(AppDbContext context, ILogger<OpeningStockController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task<int> GetCurrentUserId()
        {
            var userIdString = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                _logger.LogWarning("User is not authenticated or UserId is invalid.");
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return userId;
        }

        private List<ItemDropDownModel> SetItemDropDown()
        {
            var userId = GetCurrentUserId().Result;
            return _context.Items
                .Include(i => i.ItemGroup)
                .Where(i => i.UserId == userId && i.ItemGroup!.Name != "Gold Metal")
                .Select(i => new ItemDropDownModel
                {
                    Id = (int)i.Id!,
                    ItemName = i.Name!,
                    GroupName = i.ItemGroup!.Name ?? ""
                })
                .ToList();
        }

        private async Task<string> GenerateSequentialBillNo(string prefix = "BILL")
        {
            var userId = await GetCurrentUserId();
            var lastBill = await _context.OpeningStocks
                .Where(os => os.BillNo.StartsWith(prefix) && os.UserId == userId)
                .OrderByDescending(os => os.BillNo)
                .Select(os => os.BillNo)
                .FirstOrDefaultAsync();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastBill))
            {
                if (!int.TryParse(lastBill.Substring(prefix.Length), out lastNumber))
                {
                    _logger.LogWarning("Failed to parse bill number: {BillNo}", lastBill);
                    lastNumber = 0;
                }
            }
            return $"{prefix}{(lastNumber + 1):D4}";
        }

        private void CalculateDerivedFields(OpeningStockModel item, string groupName)
        {
            if (groupName == "PC Gold Jewelry")
            {
                item.NetWt = 0;
                item.TW = 0;
                item.Fine = 0;
                item.Amount = (item.Pc ?? 0) * (item.Rate ?? 0);
            }
            else
            {
                item.NetWt = (item.Weight ?? 0) - (item.Less ?? 0);
                item.TW = (item.Tunch ?? 0) + (item.Wastage ?? 0);
                item.Fine = (item.NetWt ?? 0) * (item.TW ?? 0) / 100;
                item.Amount = (item.Fine ?? 0) * (item.Rate ?? 0);
            }
        }

        private async Task<bool> IsOpeningStockAdded()
        {
            var userId = await GetCurrentUserId();
            return await _context.OpeningStocks.AnyAsync(os => os.UserId == userId);
        }

        [HttpGet]
        public async Task<IActionResult> AddOpeningStock()
        {
            if (await IsOpeningStockAdded())
            {
                _logger.LogInformation("Opening stock already added for user. Redirecting to ViewStock.");
                TempData["Message"] = "Opening stock has already been added. Use 'New Stock' to add more.";
                return RedirectToAction("ViewStock");
            }
            ViewBag.ItemDropDown = SetItemDropDown();
            var model = new OpeningStockViewModel
            {
                BillNo = await GenerateSequentialBillNo("STOCK"),
                Date = DateTime.Now,
                UserId = await GetCurrentUserId(),
                Items = new List<OpeningStockModel> { new OpeningStockModel() }
            };
            ViewBag.Action = "AddOpeningStock";
            ViewBag.Title = "Add Opening Stock";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOpeningStock(OpeningStockViewModel model)
        {
            var userId = await GetCurrentUserId();
            if (string.IsNullOrEmpty(model.BillNo) || !model.BillNo.StartsWith("STOCK"))
            {
                if (!await IsOpeningStockAdded())
                {
                    model.BillNo = await GenerateSequentialBillNo("STOCK");
                }
                else
                {
                    model.BillNo = await GenerateSequentialBillNo("NEW");
                }
            }
            model.UserId = userId;

            ViewBag.ItemDropDown = SetItemDropDown();
            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            if (ModelState.ContainsKey("Items.Item")) ModelState.Remove("Items.Item");
            if (ModelState.ContainsKey("Items.User")) ModelState.Remove("Items.User");
            for (int i = 0; i < model.Items.Count; i++)
            {
                if (ModelState.ContainsKey($"Items[{i}].Item")) ModelState.Remove($"Items[{i}].Item");
                if (ModelState.ContainsKey($"Items[{i}].User")) ModelState.Remove($"Items[{i}].User");
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
            }
            else
            {
                foreach (var item in model.Items)
                {
                    if (item.ItemId == 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Item is required.");
                        continue;
                    }

                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Selected item does not exist or is not associated with your user.");
                        continue;
                    }

                    string groupName = itemGroups[item.ItemId];
                    switch (groupName)
                    {
                        case "Gold Jewelry":
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Tunch.HasValue || item.Tunch <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            break;
                        case "PC Gold Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                        case "PC/Weight Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Tunch.HasValue || item.Tunch <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for AddOpeningStock: {Errors}", string.Join("; ", errors));
                return Json(new { success = false, error = "Validation failed: " + string.Join("; ", errors) });
            }

            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogError("UserId {UserId} does not exist in the Users table.", userId);
                    return Json(new { success = false, error = "Invalid user. Please log in again." });
                }

                var existingStocks = await _context.OpeningStocks
                    .Where(os => os.UserId == userId)
                    .ToListAsync();

                foreach (var item in model.Items)
                {
                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        _logger.LogError("ItemId {ItemId} does not exist or does not belong to UserId: {UserId}.", item.ItemId, userId);
                        return Json(new { success = false, error = $"Invalid item selected: ItemId {item.ItemId} does not exist or is not associated with your user." });
                    }

                    string groupName = itemGroups[item.ItemId];
                    item.NetWt = (item.Weight ?? 0) - (item.Less ?? 0);
                    item.TW = (item.Tunch ?? 0) + (item.Wastage ?? 0);
                    item.Fine = groupName == "PC Gold Jewelry" ? 0 : (item.NetWt ?? 0) * (item.TW ?? 0) / 100;
                    item.Amount = groupName == "PC Gold Jewelry" ? (item.Pc ?? 0) * (item.Rate ?? 0) : (item.Fine ?? 0) * (item.Rate ?? 0);

                    var matchingStock = existingStocks.FirstOrDefault(os =>
                        os.ItemId == item.ItemId &&
                        os.Tunch == item.Tunch &&
                        os.Wastage == item.Wastage &&
                        os.Rate == item.Rate);

                    if (matchingStock != null)
                    {
                        matchingStock.Weight = (matchingStock.Weight ?? 0) + (item.Weight ?? 0);
                        matchingStock.Less = (matchingStock.Less ?? 0) + (item.Less ?? 0);
                        matchingStock.NetWt = (matchingStock.NetWt ?? 0) + (item.NetWt ?? 0);
                        matchingStock.Pc = (matchingStock.Pc ?? 0) + (item.Pc ?? 0);
                        matchingStock.Fine = (matchingStock.Fine ?? 0) + (item.Fine ?? 0);
                        matchingStock.Amount = (matchingStock.Amount ?? 0) + (item.Amount ?? 0);
                        matchingStock.LastUpdated = DateTime.Now;
                        matchingStock.BillNo = model.BillNo;
                        matchingStock.Date = model.Date;
                        matchingStock.Narration = model.Narration;
                    }
                    else
                    {
                        item.BillNo = model.BillNo;
                        item.Date = model.Date;
                        item.Narration = model.Narration;
                        item.LastUpdated = DateTime.Now;
                        item.UserId = userId;
                        _context.OpeningStocks.Add(item);
                    }
                }

                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for BillNo: {BillNo}", model.BillNo);
                    return Json(new { success = false, error = "No changes were saved to the database." });
                }

                _logger.LogInformation("Successfully saved opening stock for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = true, message = $"Opening stock for BillNo {model.BillNo} added successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving opening stock for BillNo: {BillNo}", model.BillNo);
                return Json(new { success = false, error = $"An error occurred while saving: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AddNewStock(string billNo = null)
        {
            ViewBag.ItemDropDown = SetItemDropDown();
            var userId = await GetCurrentUserId();

            OpeningStockViewModel model;
            if (!string.IsNullOrEmpty(billNo))
            {
                var stocks = await _context.OpeningStocks
                    .Where(os => os.BillNo == billNo && os.UserId == userId)
                    .ToListAsync();

                if (!stocks.Any())
                {
                    _logger.LogWarning("No stock entries found for BillNo: {BillNo}, UserId: {UserId}", billNo, userId);
                    return NotFound();
                }

                model = new OpeningStockViewModel
                {
                    BillNo = billNo,
                    Date = stocks.First().Date,
                    Narration = stocks.First().Narration,
                    UserId = userId,
                    Items = stocks
                };
                ViewBag.Title = "Edit New Stock";
            }
            else
            {
                model = new OpeningStockViewModel
                {
                    BillNo = await GenerateSequentialBillNo("NEW"),
                    Date = DateTime.Now,
                    UserId = userId,
                    Items = new List<OpeningStockModel> { new OpeningStockModel() }
                };
                ViewBag.Title = "Add New Stock";
            }

            ViewBag.Action = "AddNewStock";
            ViewBag.AllowNewStock = true;
            return View("AddOpeningStock", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNewStock(OpeningStockViewModel model)
        {
            var userId = await GetCurrentUserId();
            model.UserId = userId;

            ViewBag.ItemDropDown = SetItemDropDown();
            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            // Clear unnecessary ModelState entries
            if (ModelState.ContainsKey("Items.Item")) ModelState.Remove("Items.Item");
            if (ModelState.ContainsKey("Items.User")) ModelState.Remove("Items.User");
            for (int i = 0; i < model.Items.Count; i++)
            {
                if (ModelState.ContainsKey($"Items[{i}].Item")) ModelState.Remove($"Items[{i}].Item");
                if (ModelState.ContainsKey($"Items[{i}].User")) ModelState.Remove($"Items[{i}].User");
            }

            // Determine if this is an edit or add operation
            bool isEditMode = await _context.OpeningStocks.AnyAsync(os => os.BillNo == model.BillNo && os.UserId == userId);
            if (!isEditMode && (string.IsNullOrEmpty(model.BillNo) || !model.BillNo.StartsWith("NEW")))
            {
                model.BillNo = await GenerateSequentialBillNo("NEW");
            }

            // Validation logic (unchanged)
            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
            }
            else
            {
                foreach (var item in model.Items)
                {
                    if (item.ItemId == 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Item is required.");
                        continue;
                    }

                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Selected item does not exist or is not associated with your user.");
                        continue;
                    }

                    string groupName = itemGroups[item.ItemId];
                    switch (groupName)
                    {
                        case "Gold Jewelry":
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Tunch.HasValue || item.Tunch <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            break;
                        case "PC Gold Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                        case "PC/Weight Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Tunch.HasValue || item.Tunch <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for AddNewStock: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join("; ", errors);
                ViewBag.Action = "AddNewStock";
                ViewBag.Title = isEditMode ? "Edit New Stock" : "Add New Stock";
                ViewBag.AllowNewStock = true;
                return View("AddOpeningStock", model);
            }

            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogError("UserId {UserId} does not exist in the Users table.", userId);
                    TempData["ErrorMessage"] = "Invalid user. Please log in again.";
                    return View("AddOpeningStock", model);
                }

                // Fetch existing stocks for this BillNo
                var existingStocks = await _context.OpeningStocks
                    .Where(os => os.BillNo == model.BillNo && os.UserId == userId)
                    .ToListAsync();

                // Process each item in the submitted model
                foreach (var item in model.Items)
                {
                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        _logger.LogError("ItemId {ItemId} does not exist or does not belong to UserId: {UserId}.", item.ItemId, userId);
                        TempData["ErrorMessage"] = $"Invalid item selected: ItemId {item.ItemId} does not exist or is not associated with your user.";
                        return View("AddOpeningStock", model);
                    }

                    string groupName = itemGroups[item.ItemId];
                    CalculateDerivedFields(item, groupName);

                    var matchingStock = existingStocks.FirstOrDefault(os =>
                        os.ItemId == item.ItemId &&
                        os.Tunch == item.Tunch &&
                        os.Wastage == item.Wastage &&
                        os.Rate == item.Rate);

                    if (matchingStock != null)
                    {
                        if (isEditMode)
                        {
                            // Edit mode: Replace existing values
                            matchingStock.Weight = item.Weight;
                            matchingStock.Less = item.Less;
                            matchingStock.NetWt = item.NetWt;
                            matchingStock.Pc = item.Pc;
                            matchingStock.Fine = item.Fine;
                            matchingStock.Amount = item.Amount;
                            matchingStock.LastUpdated = DateTime.Now;
                            matchingStock.Date = model.Date;
                            matchingStock.Narration = model.Narration;
                        }
                        else
                        {
                            // Add mode: Accumulate values
                            matchingStock.Weight = (matchingStock.Weight ?? 0) + (item.Weight ?? 0);
                            matchingStock.Less = (matchingStock.Less ?? 0) + (item.Less ?? 0);
                            matchingStock.NetWt = (matchingStock.NetWt ?? 0) + (item.NetWt ?? 0);
                            matchingStock.Pc = (matchingStock.Pc ?? 0) + (item.Pc ?? 0);
                            matchingStock.Fine = (matchingStock.Fine ?? 0) + (item.Fine ?? 0);
                            matchingStock.Amount = (matchingStock.Amount ?? 0) + (item.Amount ?? 0);
                            matchingStock.LastUpdated = DateTime.Now;
                            matchingStock.Date = model.Date;
                            matchingStock.Narration = model.Narration;
                        }
                        existingStocks.Remove(matchingStock); // Remove from list to avoid deletion later
                    }
                    else
                    {
                        // Add new stock entry
                        item.BillNo = model.BillNo;
                        item.Date = model.Date;
                        item.Narration = model.Narration;
                        item.LastUpdated = DateTime.Now;
                        item.UserId = userId;
                        _context.OpeningStocks.Add(item);
                    }
                }

                // In edit mode, remove any remaining stocks that weren't updated (deleted by user)
                if (isEditMode && existingStocks.Any())
                {
                    _context.OpeningStocks.RemoveRange(existingStocks);
                }

                int rowsAffected = await _context.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for BillNo: {BillNo}", model.BillNo);
                    TempData["ErrorMessage"] = "No changes were saved to the database.";
                    return View("AddOpeningStock", model);
                }

                _logger.LogInformation("Successfully {Action} stock for BillNo: {BillNo}", isEditMode ? "updated" : "added", model.BillNo);
                TempData["SweetAlertMessage"] = $"Stock for BillNo {model.BillNo} {(isEditMode ? "updated" : "added")} successfully!";
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving new stock for BillNo: {BillNo}", model.BillNo);
                TempData["ErrorMessage"] = $"An error occurred while saving: {ex.Message}";
                return View("AddOpeningStock", model);
            }
        }


        [HttpGet]
        public async Task<IActionResult> EditOpeningStock(string billNo)
        {
            if (string.IsNullOrEmpty(billNo))
            {
                _logger.LogWarning("BillNo is null or empty in EditOpeningStock.");
                return NotFound();
            }

            var userId = await GetCurrentUserId();
            var stocks = await _context.OpeningStocks
                .Where(os => os.BillNo == billNo && os.UserId == userId)
                .ToListAsync();

            if (!stocks.Any())
            {
                _logger.LogWarning("No stock entries found for BillNo: {BillNo}, UserId: {UserId}", billNo, userId);
                return NotFound();
            }

            var model = new OpeningStockViewModel
            {
                BillNo = billNo,
                Date = stocks.First().Date,
                Narration = stocks.First().Narration,
                UserId = userId,
                Items = stocks
            };

            ViewBag.ItemDropDown = SetItemDropDown();
            ViewBag.Action = "EditOpeningStock";
            ViewBag.Title = "Edit Stock";
            ViewBag.AllowNewStock = true;
            ViewBag.IsEdit = true;
            ViewBag.BillNo = billNo;

            return View("AddOpeningStock", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOpeningStock(OpeningStockViewModel model)
        {
            var userId = await GetCurrentUserId();
            model.UserId = userId;

            ViewBag.ItemDropDown = SetItemDropDown();
            var itemGroups = SetItemDropDown().ToDictionary(i => i.Id, i => i.GroupName);

            // Clear unnecessary ModelState entries
            if (ModelState.ContainsKey("Items.Item")) ModelState.Remove("Items.Item");
            if (ModelState.ContainsKey("Items.User")) ModelState.Remove("Items.User");
            for (int i = 0; i < model.Items.Count; i++)
            {
                if (ModelState.ContainsKey($"Items[{i}].Item")) ModelState.Remove($"Items[{i}].Item");
                if (ModelState.ContainsKey($"Items[{i}].User")) ModelState.Remove($"Items[{i}].User");
            }

            // Validation logic (unchanged)
            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("Items", "At least one item is required.");
            }
            else
            {
                foreach (var item in model.Items)
                {
                    if (item.ItemId == 0)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Item is required.");
                        continue;
                    }

                    var itemExists = await _context.Items.AnyAsync(i => i.Id == item.ItemId && i.UserId == userId);
                    if (!itemExists)
                    {
                        ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].ItemId", "Selected item does not exist or is not associated with your user.");
                        continue;
                    }

                    string groupName = itemGroups[item.ItemId];
                    switch (groupName)
                    {
                        case "Gold Jewelry":
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Tunch.HasValue || item.Tunch <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            break;
                        case "PC Gold Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                        case "PC/Weight Jewelry":
                            if (!item.Pc.HasValue || item.Pc <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Pc", "Pc is required.");
                            if (!item.Weight.HasValue || item.Weight <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Weight", "Gross Weight is required.");
                            if (!item.Tunch.HasValue || item.Tunch <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Tunch", "Tunch is required.");
                            if (!item.Wastage.HasValue || item.Wastage <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Wastage", "Wastage is required.");
                            if (!item.Rate.HasValue || item.Rate <= 0) ModelState.AddModelError($"Items[{model.Items.IndexOf(item)}].Rate", "Rate is required.");
                            break;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for EditOpeningStock: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join("; ", errors);
                ViewBag.Action = "EditOpeningStock";
                ViewBag.Title = "Edit Stock";
                ViewBag.AllowNewStock = true;
                ViewBag.IsEdit = true;
                ViewBag.BillNo = model.BillNo;
                return View("AddOpeningStock", model);
            }

            try
            {
                var existingStocks = await _context.OpeningStocks
                    .Where(os => os.BillNo == model.BillNo && os.UserId == userId)
                    .ToListAsync();

                // Update or add items
                foreach (var item in model.Items)
                {
                    string groupName = itemGroups[item.ItemId];
                    CalculateDerivedFields(item, groupName);

                    var matchingStock = existingStocks.FirstOrDefault(os =>
                        os.ItemId == item.ItemId &&
                        os.Tunch == item.Tunch &&
                        os.Wastage == item.Wastage &&
                        os.Rate == item.Rate);

                    if (matchingStock != null)
                    {
                        // Update existing stock
                        matchingStock.Weight = item.Weight;
                        matchingStock.Less = item.Less;
                        matchingStock.NetWt = item.NetWt;
                        matchingStock.Pc = item.Pc;
                        matchingStock.Fine = item.Fine;
                        matchingStock.Amount = item.Amount;
                        matchingStock.LastUpdated = DateTime.Now;
                        matchingStock.Date = model.Date;
                        matchingStock.Narration = model.Narration;
                        existingStocks.Remove(matchingStock); // Remove from list to avoid deletion later
                    }
                    else
                    {
                        // Add new stock
                        item.BillNo = model.BillNo;
                        item.Date = model.Date;
                        item.Narration = model.Narration;
                        item.LastUpdated = DateTime.Now;
                        item.UserId = userId;
                        _context.OpeningStocks.Add(item);
                    }
                }

                // Remove any remaining stocks that weren't updated (deleted by user)
                if (existingStocks.Any())
                {
                    _context.OpeningStocks.RemoveRange(existingStocks);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated stock for BillNo: {BillNo}", model.BillNo);
                TempData["SweetAlertMessage"] = $"Stock for BillNo {model.BillNo} updated successfully!";
                return RedirectToAction("ViewStock");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating stock for BillNo: {BillNo}", model.BillNo);
                TempData["ErrorMessage"] = $"An error occurred while updating: {ex.Message}";
                ViewBag.Action = "EditOpeningStock";
                ViewBag.Title = "Edit Stock";
                ViewBag.AllowNewStock = true;
                ViewBag.IsEdit = true;
                ViewBag.BillNo = model.BillNo;
                return View("AddOpeningStock", model);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOpeningStock(string billNo)
        {
            var userId = await GetCurrentUserId();

            try
            {
                var stocks = await _context.OpeningStocks
                    .Where(os => os.BillNo == billNo && os.UserId == userId)
                    .ToListAsync();

                if (!stocks.Any())
                {
                    _logger.LogWarning("No stock found for BillNo: {BillNo} and UserId: {UserId}", billNo, userId);
                    return Json(new { success = false, error = "No stock found with this BillNo." });
                }

                _context.OpeningStocks.RemoveRange(stocks);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Stock deleted successfully for BillNo: {BillNo}", billNo);
                return Json(new { success = true, redirectUrl = Url.Action("ViewStock"), message = "Stock deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting stock for BillNo: {BillNo}", billNo);
                return Json(new { success = false, error = $"An error occurred while deleting: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewStock(int page = 1)
        {
            var userId = await GetCurrentUserId();

            // Fetch stock data
            var stocks = await _context.OpeningStocks
                .Where(os => os.UserId == userId)
                .Join(
                    _context.Items,
                    stock => stock.ItemId,
                    item => item.Id,
                    (stock, item) => new
                    {
                        stock.BillNo,
                        ItemName = item.Name,
                        stock.Pc,
                        stock.Weight,
                        stock.Less,
                        stock.NetWt,
                        stock.Tunch,
                        stock.Wastage,
                        stock.Fine,
                        stock.Amount,
                        stock.LastUpdated
                    }
                )
                .OrderBy(s => s.LastUpdated)
                .ToListAsync();

            // Create DataTable
            DataTable dt = new DataTable("OpeningStock");
            dt.Columns.Add("BillNo", typeof(string));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("Pc", typeof(int));
            dt.Columns.Add("Weight", typeof(decimal));
            dt.Columns.Add("Less", typeof(decimal));
            dt.Columns.Add("NetWt", typeof(decimal));
            dt.Columns.Add("Tunch", typeof(decimal));
            dt.Columns.Add("Wastage", typeof(decimal));
            dt.Columns.Add("Fine", typeof(decimal));
            dt.Columns.Add("Amount", typeof(decimal));
            dt.Columns.Add("LastUpdated", typeof(DateTime));

            // Populate DataTable
            foreach (var stock in stocks)
            {
                dt.Rows.Add(
                    stock.BillNo,
                    stock.ItemName,
                    stock.Pc ?? 0,
                    stock.Weight ?? 0m,
                    stock.Less ?? 0m,
                    stock.NetWt ?? 0m,
                    stock.Tunch ?? 0m,
                    stock.Wastage ?? 0m,
                    stock.Fine ?? 0m,
                    stock.Amount ?? 0m,
                    stock.LastUpdated
                );
            }

            // Pagination logic
            int pageSize = 10; // Adjust as needed
            int totalRecords = dt.Rows.Count;
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            var pagedData = dt.AsEnumerable()
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            // Copy paged data to a new DataTable
            DataTable pagedDt = dt.Clone();
            foreach (var row in pagedData)
            {
                pagedDt.ImportRow(row);
            }

            // Set ViewBag for pagination
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(pagedDt);
        }
    }
}