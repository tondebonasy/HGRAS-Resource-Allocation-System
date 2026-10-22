using System.Linq;
using System.Net;
using System.Web.Mvc;
using ResourceAllocationAdmin.Models;
namespace ResourceAllocationAdmin.Controllers {
 [Authorize(Roles="ADMIN")]
 public class AspNetRolesController:Controller {
  private readonly ResourceDistrubitionDBEntities db=new ResourceDistrubitionDBEntities();
  public ActionResult Index(){return View(db.AspNetRoles.ToList());}
  public ActionResult Details(string id){return RedirectToAction("Index");}
  public ActionResult Create(){return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Workflow stages and access roles are system-managed. Assign roles through user management.");}
  public ActionResult Edit(string id){return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Workflow stages and access roles are system-managed.");}
  public ActionResult Delete(string id){return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Workflow stages and access roles are system-managed.");}
  protected override void Dispose(bool disposing){if(disposing)db.Dispose();base.Dispose(disposing);}
 }
}
