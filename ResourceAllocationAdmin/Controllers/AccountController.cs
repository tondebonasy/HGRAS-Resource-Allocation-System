using System;
using System.Diagnostics; // Added for Debug.WriteLine
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using ResourceAllocationAdmin.Models;

namespace ResourceAllocationAdmin.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController() { }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get { return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>(); }
            private set { _signInManager = value; }
        }

        public ApplicationUserManager UserManager
        {
            get { return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>(); }
            private set { _userManager = value; }
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.FirstAdministrator = !HasAdministrator();
            return View();
        }

        // POST: /Account/Login
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            if (!await UserManager.IsEmailConfirmedAsync(user.Id))
            {
                ModelState.AddModelError("", "You must confirm your email before you can sign in.");
                return View(model);
            }

            var result = await SignInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, shouldLockout: true);

            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }

        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            if (!await SignInManager.HasBeenVerifiedAsync()) return View("Error");
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        // POST: /Account/VerifyCode
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await SignInManager.TwoFactorSignInAsync(
                model.Provider, model.Code, isPersistent: model.RememberMe, rememberBrowser: model.RememberBrowser);

            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(model.ReturnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                default:
                    ModelState.AddModelError("", "Invalid code.");
                    return View(model);
            }
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            if (HasAdministrator() && !User.IsInRole("ADMIN")) return new HttpStatusCodeResult(403, "Staff registration is managed by an administrator.");
            ViewBag.FirstAdministrator = !HasAdministrator();
            return View();
        }

        private bool HasAdministrator()
        {
            var context = HttpContext.GetOwinContext().Get<ApplicationDbContext>();
            return context.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.AspNetUserRoles ur JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='ADMIN'").Single() > 0;
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            var context = HttpContext.GetOwinContext().Get<ApplicationDbContext>();
            using (var transaction = context.Database.BeginTransaction())
            {
                context.Database.ExecuteSqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='PGH.FirstAdministrator', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result<0 THROW 50001, 'Administrator setup is busy. Please retry.', 1;");
                var first = !HasAdministrator();
                if (!first && !User.IsInRole("ADMIN")) return new HttpStatusCodeResult(403, "Staff registration is managed by an administrator.");
                ViewBag.FirstAdministrator = first;
                if (!ModelState.IsValid) return View(model);
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (!result.Succeeded) { AddErrors(result); return View(model); }
                if (first)
                {
                    result = await UserManager.AddToRoleAsync(user.Id, "ADMIN");
                    if (!result.Succeeded) { AddErrors(result); return View(model); }
                }
                transaction.Commit();
                if (!first) return RedirectToAction("Edit", "AspNetUsers", new { id = user.Id });
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                return RedirectToAction("Index", "Network");
            }
        }
        // GET: /Account/DisplayEmail
        [AllowAnonymous]
        public ActionResult DisplayEmail(string email = null)
        {
            ViewBag.Email = email;
            ViewBag.Message = string.IsNullOrEmpty(email)
                ? "Please check your email for a confirmation link."
                : "Please check " + email + " for a confirmation link.";
            return View();
        }

        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code)) return View("Error");
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword() { return View(); }

        // POST: /Account/ForgotPassword
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
            {
                // Do not reveal existence
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            var code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
            var callbackUrl = Url.Action("ResetPassword", "Account",
                new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);

            var html = "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>.";
            var sent = await TrySendEmailAsync(user.Id, "Reset Password", html);

            if (sent) return RedirectToAction("ForgotPasswordConfirmation");

            ModelState.AddModelError("", "We couldn't send the reset email. Try again later.");
            return View(model);
        }

        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation() { return View(); }

        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            if (string.IsNullOrEmpty(code)) return View("Error");
            return View(new ResetPasswordViewModel { Code = code });
        }

        // POST: /Account/ResetPassword
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null) return RedirectToAction("ResetPasswordConfirmation");

            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded) return RedirectToAction("ResetPasswordConfirmation");

            AddErrors(result);
            return View(model);
        }

        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation() { return View(); }

        // POST: /Account/ExternalLogin
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null) return RedirectToAction("Login");

            var user = await UserManager.FindAsync(loginInfo.Login);
            if (user != null)
            {
                if (!await UserManager.IsEmailConfirmedAsync(user.Id))
                {
                    var code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    var callbackUrl = Url.Action("ConfirmEmail", "Account",
                        new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);

                    var html = "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>.";
                    var sent = await TrySendEmailAsync(user.Id, "Confirm your account", html);

                    if (sent) return RedirectToAction("DisplayEmail", new { email = user.Email });

                    ModelState.AddModelError("", "We couldn't send the confirmation email. Try again later.");
                    return RedirectToAction("Login");
                }

                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                return RedirectToLocal(returnUrl);
            }


            ViewBag.ReturnUrl = returnUrl;
            ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
            return View("ExternalLoginConfirmation",
                new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
        }

        // POST: /Account/ExternalLoginConfirmation
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            return new HttpStatusCodeResult(403, "External staff registration is disabled. Contact your administrator.");
        }

        // POST: /Account/LogOff
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Network");
        }

        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure() { return View(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null) { _userManager.Dispose(); _userManager = null; }
                if (_signInManager != null) { _signInManager.Dispose(); _signInManager = null; }
            }
            base.Dispose(disposing);
        }

        #region Helpers

        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get { return HttpContext.GetOwinContext().Authentication; }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Network");
        }

        // This method contains our error logging
        private async Task<bool> TrySendEmailAsync(string userId, string subject, string htmlBody)
        {
            try
            {
                await UserManager.SendEmailAsync(userId, subject, htmlBody);
                return true;
            }
            catch (Exception ex)
            {
                // This new line will print the full error to your Output window
                System.Diagnostics.Debug.WriteLine("!!! EMAIL ERROR: " + ex.ToString());

                Trace.TraceError("[EmailSendFailure] subject={0} userId={1} error={2}", subject, userId, ex.ToString());
                return false;
            }
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri) : this(provider, redirectUri, null) { }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; private set; }
            public string RedirectUri { get; private set; }
            public string UserId { get; private set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null) properties.Dictionary[XsrfKey] = UserId;
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        #endregion
    }
}
