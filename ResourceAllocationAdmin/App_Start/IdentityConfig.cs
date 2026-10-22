using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using ResourceAllocationAdmin.Models;

namespace ResourceAllocationAdmin
{
    public class EmailService : IIdentityMessageService
    {
        // Outlook.com/Hotmail => smtp-mail.outlook.com:587
        // Microsoft 365 (work/school) => smtp.office365.com:587
        public async Task SendAsync(IdentityMessage message)
        {
            if (System.Configuration.ConfigurationManager.AppSettings["DemoMode"] == "true") return;
            var host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
            int port; if (!int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out port)) port = 587;
            var user = ConfigurationManager.AppSettings["SmtpUser"];
            var pass = ConfigurationManager.AppSettings["SmtpPass"];
            var from = ConfigurationManager.AppSettings["SmtpFrom"] ?? user;

            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(from, "HGRAS");
                mail.To.Add(message.Destination);
                mail.Subject = message.Subject;
                mail.Body = message.Body;
                mail.IsBodyHtml = true;

                using (var client = new SmtpClient(host, port))
                {
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.UseDefaultCredentials = false;
                    client.EnableSsl = true; // STARTTLS on 587
                    client.Credentials = new NetworkCredential(user, pass);

                    await client.SendMailAsync(mail); // bubble exceptions
                }
            }
        }
    }

    public class SmsService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message) { return Task.FromResult(0); }
    }

    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store) : base(store) { }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));

            manager.UserValidator = new UserValidator<ApplicationUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };

            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true
            };

            manager.UserLockoutEnabledByDefault = true;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            manager.MaxFailedAccessAttemptsBeforeLockout = 5;

            manager.RegisterTwoFactorProvider("Phone Code", new PhoneNumberTokenProvider<ApplicationUser>
            {
                MessageFormat = "Your security code is {0}"
            });
            manager.RegisterTwoFactorProvider("Email Code", new EmailTokenProvider<ApplicationUser>
            {
                Subject = "Security Code",
                BodyFormat = "Your security code is {0}"
            });

            manager.EmailService = new EmailService();
            manager.SmsService = new SmsService();

            var dpp = options.DataProtectionProvider;
            if (dpp != null)
                manager.UserTokenProvider = new DataProtectorTokenProvider<ApplicationUser>(dpp.Create("ASP.NET Identity"));

            return manager;
        }
    }

    public class ApplicationSignInManager : SignInManager<ApplicationUser, string>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager, IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager) { }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(ApplicationUser user)
        {
            return user.GenerateUserIdentityAsync((ApplicationUserManager)UserManager);
        }

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
        {
            return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
        }
    }
}

