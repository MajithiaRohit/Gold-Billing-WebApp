using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Gold_Billing_WebApp.Models;
using Gold_Billing_Web_App.Models.ViewModels;

namespace Gold_Billing_Web_App.Controllers
{
    public class UserAccountController : Controller
    {
        private readonly AppDbContext _context;

        public UserAccountController(AppDbContext context)
        {
            _context = context;
        }

        #region Login Actions

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            if (user == null)
            {
                ViewBag.UsernameError = "Username is incorrect";
                return View(model);
            }

            model.Password = model.Password.Trim();
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.Password);
            if (!isPasswordValid)
            {
                ViewBag.PasswordError = "Password is incorrect";
                return View(model);
            }

            HttpContext.Session.SetString(CommonVariable.UserId, user.Id.ToString());
            HttpContext.Session.SetString(CommonVariable.FullName, user.FullName ?? "");
            HttpContext.Session.SetString(CommonVariable.GodName1, user.GodName1 ?? "");
            HttpContext.Session.SetString(CommonVariable.GodName2, user.GodName2 ?? "");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? "")
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        #endregion

        #region Register Actions

        [HttpGet]
        public IActionResult Register()
        {
            ViewData["Title"] = "Register";
            ViewBag.IsEdit = false;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Passwords do not match");
                    ViewData["Title"] = "Register";
                    ViewBag.IsEdit = false;
                    return View(model);
                }

                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    ViewBag.UsernameError = "Username already exists";
                    ViewData["Title"] = "Register";
                    ViewBag.IsEdit = false;
                    return View(model);
                }

                model.Password = model.Password.Trim();
                var user = new UserAccountModel
                {
                    FullName = model.FullName,
                    Username = model.Username,
                    Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    CompanyName = model.CompanyName,
                    CompanyAddress = model.CompanyAddress,
                    Email = model.Email,
                    MobileNo = model.MobileNo,
                    GstNumber = model.GstNumber,
                    GodName1 = model.GodName1,
                    GodName2 = model.GodName2
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login");
            }

            ViewData["Title"] = "Register";
            ViewBag.IsEdit = false;
            return View(model);
        }

        #endregion

        #region VerifyUsername Actions

        [HttpGet]
        public IActionResult VerifyUsername()
        {
            return View(new VerifyUsernameViewModel { IsUsernameVerified = false });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyUsername(VerifyUsernameViewModel model)
        {
            if (TempData["IsUsernameVerified"] != null)
            {
                model.IsUsernameVerified = (bool)TempData["IsUsernameVerified"]!;
            }

            if (model.IsUsernameVerified)
            {
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "New password is required.");
                }
                else if (model.NewPassword.Length < 6 || model.NewPassword.Length > 100)
                {
                    ModelState.AddModelError("NewPassword", "Password must be between 6 and 100 characters.");
                }

                if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
                {
                    ModelState.AddModelError("ConfirmPassword", "Confirm password is required.");
                }
                else if (model.NewPassword != model.ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "The new password and confirm password do not match.");
                }
            }

            if (!ModelState.IsValid)
            {
                if (model.IsUsernameVerified)
                {
                    TempData["IsUsernameVerified"] = true;
                }
                return View(model);
            }

            if (!model.IsUsernameVerified)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());
                if (user == null)
                {
                    ViewBag.UsernameError = "Username not found.";
                    return View(model);
                }

                model.IsUsernameVerified = true;
                TempData["IsUsernameVerified"] = true;
                return View(model);
            }
            else
            {
                if (model.NewPassword != model.ConfirmPassword)
                {
                    ViewBag.PasswordError = "New password and confirm password do not match.";
                    TempData["IsUsernameVerified"] = true;
                    return View(model);
                }

                model.NewPassword = model.NewPassword!.Trim();
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, 12);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());
                if (user == null)
                {
                    ViewBag.UsernameError = "User not found.";
                    TempData["IsUsernameVerified"] = true;
                    return View(model);
                }

                user.Password = hashedPassword;
                var rowsAffected = await _context.SaveChangesAsync();
                var verificationResult = BCrypt.Net.BCrypt.Verify(model.NewPassword, user.Password);

                if (rowsAffected > 0 && verificationResult)
                {
                    TempData["SuccessMessage"] = "Password reset successfully!";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login");
                }

                ViewBag.PasswordError = "An error occurred while resetting the password.";
                TempData["IsUsernameVerified"] = true;
                return View(model);
            }
        }

        #endregion

        #region EditUser Actions

        [HttpGet]
        public async Task<IActionResult> EditUser()
        {
            // Get userId from session or claims
            var userId = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // If userId is null or empty, redirect to login regardless of authentication status
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            // userId is guaranteed to be non-null here
            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null)
            {
                return NotFound();
            }

            var model = new RegisterViewModel
            {
                FullName = user.FullName!,
                Username = user.Username,
                CompanyName = user.CompanyName,
                CompanyAddress = user.CompanyAddress,
                Email = user.Email,
                MobileNo = user.MobileNo,
                GstNumber = user.GstNumber,
                GodName1 = user.GodName1,
                GodName2 = user.GodName2
            };

            ViewData["Title"] = "Edit Profile";
            ViewBag.IsEdit = true;
            return View("Register", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(RegisterViewModel model)
        {
            if (!string.IsNullOrEmpty(model.Password))
            {
                return RedirectToAction("VerifyUsername");
            }

            if (ModelState.IsValid)
            {
                // Get userId from session or claims
                var userId = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // If userId is null or empty, redirect to login regardless of authentication status
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login");
                }

                // userId is guaranteed to be non-null here
                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                {
                    return NotFound();
                }

                if (await _context.Users.AnyAsync(u => u.Username == model.Username && u.Id != user.Id))
                {
                    ModelState.AddModelError("Username", "Username already exists");
                    ViewData["Title"] = "Edit Profile";
                    ViewBag.IsEdit = true;
                    return View("Register", model);
                }

                user.FullName = model.FullName;
                user.Username = model.Username;
                user.CompanyName = model.CompanyName;
                user.CompanyAddress = model.CompanyAddress;
                user.Email = model.Email;
                user.MobileNo = model.MobileNo;
                user.GstNumber = model.GstNumber;
                user.GodName1 = model.GodName1 ?? "Default God 1";
                user.GodName2 = model.GodName2 ?? "Default God 2";

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetString(CommonVariable.FullName, user.FullName ?? "");
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName ?? "")
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = "Edit Profile";
            ViewBag.IsEdit = true;
            return View("Register", model);
        }

        #endregion

        #region Logout Action

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        #endregion
    }
}