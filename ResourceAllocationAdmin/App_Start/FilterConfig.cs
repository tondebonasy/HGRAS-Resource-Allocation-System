using System.Web;
using System.Web.Mvc;

namespace ResourceAllocationAdmin
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new ExpiredFormFilter());
            filters.Add(new HandleErrorAttribute());
        }
    }
    public sealed class ExpiredFormFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (!(context.Exception is HttpAntiForgeryException)) return;
            // Reject the request; never replay a submitted mutation.
            context.ExceptionHandled = true;
            context.HttpContext.Response.StatusCode = 400;
            context.HttpContext.Response.TrySkipIisCustomErrors = true;
            context.Result = new ContentResult { ContentType = "text/html", Content = "<!doctype html><html><head><title>Form expired</title></head><body style='font-family:system-ui;padding:3rem;max-width:40rem;margin:auto'><h1>Please reopen this form</h1><p>Your security token has expired or belongs to another portal. Nothing was submitted. Open the page again and retry.</p><a href='/Account/Login'>Return to sign in</a></body></html>" };
        }
    }}
