using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Gold_Billing_Web_App.Session;

namespace Gold_Billing_Web_App.Session
{
    public class LoginCheckAccess : ActionFilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            var controller = filterContext.RouteData.Values["controller"]?.ToString();
            var action = filterContext.RouteData.Values["action"]?.ToString();

            System.Diagnostics.Debug.WriteLine($"LoginCheckAccess: Controller = {controller}, Action = {action}");

            // Allow unauthenticated access to Login, Register, and VerifyUsername
            if (controller == "UserAccount" &&
                (action == "Login" || action == "Register" || action == "VerifyUsername"))
            {
                return;
            }

            // Check session or authentication
            if (filterContext.HttpContext.Session.GetString(CommonVariable.UserId) == null &&
                !filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("User not authenticated, redirecting to Login");
                filterContext.Result = new RedirectToActionResult("Login", "UserAccount", null);
            }
        }

        public override void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.HttpContext.Response.Headers["Expires"] = "-1";
            context.HttpContext.Response.Headers["Pragma"] = "no-cache";
            base.OnResultExecuting(context);
        }
    }
}