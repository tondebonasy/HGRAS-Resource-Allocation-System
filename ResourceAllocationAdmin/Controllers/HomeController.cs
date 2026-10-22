using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using ResourceAllocationAdmin.Models;

namespace ResourceAllocationAdmin.Controllers
{
    [Authorize(Roles = "ADMIN,APPROVER,RELEASER,DISPATCHER")]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            using(var db=new ResourceDistrubitionDBEntities()) {
                var today=DateTime.Today;var tomorrow=today.AddDays(1);
                var total=db.Requests.Count();
                var completed=db.Requests.Count(r=>r.RequestStage!=null&&r.RequestStage.Name=="COMPLETED");
                var rejected=db.Requests.Count(r=>r.RequestStage!=null&&r.RequestStage.Name=="REJECTED");
                return View(new OperationsOverview {
                    Total=total,Completed=completed,Active=total-completed-rejected,
                    SubmittedToday=db.Requests.Count(r=>r.DateRequested>=today&&r.DateRequested<tomorrow),
                    Organisations=db.Companies.Count(),Resources=db.Resources.Count(),LowStock=db.Resources.Count(r=>r.Quantity.HasValue&&r.Quantity<=10),
                    Recent=db.Requests.Include(r=>r.Company).Include(r=>r.RequestStage).OrderByDescending(r=>r.DateRequested).ThenByDescending(r=>r.Id).Take(6).ToList(),
                    Stages=db.Requests.GroupBy(r=>r.RequestStage==null?"Awaiting review":r.RequestStage.Name).Select(g=>new StageSummary{Name=g.Key,Count=g.Count()}).OrderByDescending(g=>g.Count).ToList()
                });
            }
        }

        public ActionResult About() { return View(); }
        public ActionResult Contact() { return View(); }
    }
}

