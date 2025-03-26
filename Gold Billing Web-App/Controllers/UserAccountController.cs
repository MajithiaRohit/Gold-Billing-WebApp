using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Gold_Billing_WebApp.Models;

namespace Gold_Billing_Web_App.Controllers
{
    public class UserAccountController : Controller
    {
        private readonly AppDbContext _context;

        public UserAccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            System.Diagnostics.Debug.WriteLine("Login GET accessed");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            System.Diagnostics.Debug.WriteLine($"Login POST: Username = '{model.Username}', Password = '{model.Password}'");
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

            // Trim the password to avoid whitespace issues
            model.Password = model.Password?.Trim();
            System.Diagnostics.Debug.WriteLine($"Trimmed Password: '{model.Password}'");
            System.Diagnostics.Debug.WriteLine($"Stored Hashed Password: {user.Password}");

            // Verify the password
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.Password);
            System.Diagnostics.Debug.WriteLine($"Password Verification Result: {isPasswordValid}");

            if (!isPasswordValid)
            {
                ViewBag.PasswordError = "Password is incorrect";
                return View(model);
            }

            HttpContext.Session.SetString(CommonVariable.UserId, user.Id.ToString());
            HttpContext.Session.SetString(CommonVariable.FullName, user.FullName);
            HttpContext.Session.SetString(CommonVariable.GodName1, user.GodName1 ?? "");
            HttpContext.Session.SetString(CommonVariable.GodName2, user.GodName2 ?? "");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            System.Diagnostics.Debug.WriteLine("Register GET accessed");
            ViewData["Title"] = "Register";
            ViewBag.IsEdit = false;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            System.Diagnostics.Debug.WriteLine($"Register POST: Username = '{model.Username}'");
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

                model.Password = model.Password?.Trim();
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

                System.Diagnostics.Debug.WriteLine($"Register Hashed Password: {user.Password}");

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login");
            }
            ViewData["Title"] = "Register";
            ViewBag.IsEdit = false;
            return View(model);
        }
        [HttpGet]
        public IActionResult VerifyUsername()
        {
            Console.WriteLine("VerifyUsername GET action called.");
            System.Diagnostics.Debug.WriteLine("VerifyUsername GET action called.");
            return View(new VerifyUsernameViewModel { IsUsernameVerified = false });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyUsername(VerifyUsernameViewModel model)
        {
            // Restore IsUsernameVerified from TempData if available
            if (TempData["IsUsernameVerified"] != null)
            {
                model.IsUsernameVerified = (bool)TempData["IsUsernameVerified"];
            }

            Console.WriteLine($"Received IsUsernameVerified: {model.IsUsernameVerified}");
            System.Diagnostics.Debug.WriteLine($"Received IsUsernameVerified: {model.IsUsernameVerified}");

            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            System.Diagnostics.Debug.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

            // Custom validation for NewPassword and ConfirmPassword when IsUsernameVerified is true
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
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"ModelState Error: {error.ErrorMessage}");
                    System.Diagnostics.Debug.WriteLine($"ModelState Error: {error.ErrorMessage}");
                }
                // Preserve IsUsernameVerified in TempData if validation fails
                if (model.IsUsernameVerified)
                {
                    TempData["IsUsernameVerified"] = true;
                }
                return View(model);
            }

            Console.WriteLine($"IsUsernameVerified: {model.IsUsernameVerified}, Username: '{model.Username}'");
            System.Diagnostics.Debug.WriteLine($"IsUsernameVerified: {model.IsUsernameVerified}, Username: '{model.Username}'");
            if (!model.IsUsernameVerified)
            {
                // Step 1: Verify the username
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());
                Console.WriteLine($"User found: {user != null}");
                System.Diagnostics.Debug.WriteLine($"User found: {user != null}");
                if (user == null)
                {
                    Console.WriteLine("Username not found.");
                    System.Diagnostics.Debug.WriteLine("Username not found.");
                    ViewBag.UsernameError = "Username not found.";
                    return View(model);
                }

                // Username verified, show password reset fields
                Console.WriteLine("Username verified, setting IsUsernameVerified to true.");
                System.Diagnostics.Debug.WriteLine("Username verified, setting IsUsernameVerified to true.");
                model.IsUsernameVerified = true;
                TempData["IsUsernameVerified"] = true;
                Console.WriteLine($"After setting, IsUsernameVerified: {model.IsUsernameVerified}");
                System.Diagnostics.Debug.WriteLine($"After setting, IsUsernameVerified: {model.IsUsernameVerified}");
                return View(model);
            }
            else
            {
                // Step 2: Reset the password
                Console.WriteLine("Entering password reset logic.");
                System.Diagnostics.Debug.WriteLine("Entering password reset logic.");

                Console.WriteLine($"NewPassword: '{model.NewPassword}', ConfirmPassword: '{model.ConfirmPassword}'");
                System.Diagnostics.Debug.WriteLine($"NewPassword: '{model.NewPassword}', ConfirmPassword: '{model.ConfirmPassword}'");
                if (model.NewPassword != model.ConfirmPassword)
                {
                    Console.WriteLine("New password and confirm password do not match.");
                    System.Diagnostics.Debug.WriteLine("New password and confirm password do not match.");
                    ViewBag.PasswordError = "New password and confirm password do not match.";
                    TempData["IsUsernameVerified"] = true; // Preserve for the next request
                    return View(model);
                }

                // Trim the password to avoid whitespace issues
                model.NewPassword = model.NewPassword?.Trim();
                Console.WriteLine($"Trimmed New Password: '{model.NewPassword}'");
                System.Diagnostics.Debug.WriteLine($"Trimmed New Password: '{model.NewPassword}'");

                // Hash the new password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, 12);
                Console.WriteLine($"New Hashed Password: '{hashedPassword}'");
                System.Diagnostics.Debug.WriteLine($"New Hashed Password: '{hashedPassword}'");

                // Update the user's password
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());
                if (user == null)
                {
                    Console.WriteLine("User not found during password reset.");
                    System.Diagnostics.Debug.WriteLine("User not found during password reset.");
                    ViewBag.UsernameError = "User not found.";
                    TempData["IsUsernameVerified"] = true; // Preserve for the next request
                    return View(model);
                }

                user.Password = hashedPassword;
                var rowsAffected = await _context.SaveChangesAsync();
                Console.WriteLine($"Rows Affected: {rowsAffected}");
                System.Diagnostics.Debug.WriteLine($"Rows Affected: {rowsAffected}");

                // Verify the saved password
                var verificationResult = BCrypt.Net.BCrypt.Verify(model.NewPassword, user.Password);
                Console.WriteLine($"Post-Save Password Verification: {verificationResult}");
                System.Diagnostics.Debug.WriteLine($"Post-Save Password Verification: {verificationResult}");

                if (rowsAffected > 0 && verificationResult)
                {
                    Console.WriteLine("Password reset successful, redirecting to Login.");
                    System.Diagnostics.Debug.WriteLine("Password reset successful, redirecting to Login.");
                    TempData["SuccessMessage"] = "Password reset successfully!";
                    return RedirectToAction("Login");
                }

                Console.WriteLine("Password reset failed.");
                System.Diagnostics.Debug.WriteLine("Password reset failed.");
                ViewBag.PasswordError = "An error occurred while resetting the password.";
                TempData["IsUsernameVerified"] = true; // Preserve for the next request
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditUser()
        {
            System.Diagnostics.Debug.WriteLine("EditUser GET accessed");
            var userId = HttpContext.Session.GetString(CommonVariable.UserId);
            if (string.IsNullOrEmpty(userId) && !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(int.Parse(userId ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value));
            if (user == null)
            {
                return NotFound();
            }

            var model = new RegisterViewModel
            {
                FullName = user.FullName,
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
            System.Diagnostics.Debug.WriteLine($"EditUser POST: Username = '{model.Username}'");
            if (!string.IsNullOrEmpty(model.Password))
            {
                System.Diagnostics.Debug.WriteLine("Password entered in EditUser, redirecting to VerifyUsername");
                return RedirectToAction("VerifyUsername");
            }

            if (ModelState.IsValid)
            {
                var userId = HttpContext.Session.GetString(CommonVariable.UserId) ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login");
                }

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

                HttpContext.Session.SetString(CommonVariable.FullName, user.FullName);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName)
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

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            System.Diagnostics.Debug.WriteLine("Logout GET accessed");
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}