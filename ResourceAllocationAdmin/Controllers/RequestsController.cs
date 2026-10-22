using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Pgh.Workflow;
using ResourceAllocationAdmin.Models;
namespace ResourceAllocationAdmin.Controllers
{
 [Authorize(Roles="ADMIN,APPROVER,RELEASER,DISPATCHER")]
 public class RequestsController : Controller
 {
  private readonly ResourceDistrubitionDBEntities db=new ResourceDistrubitionDBEntities();
  private Request Find(long? id) { return db.Requests.Include(r=>r.Company).Include(r=>r.RequestStage).Include(r=>r.ResourceRequests.Select(i=>i.Resource)).FirstOrDefault(r=>r.Id==id); }
  private void Populate(Request request) {
   var allowed=RequestWorkflow.NextStages(request.RequestStage == null ? null : request.RequestStage.Name,User);
   ViewBag.StageId=new SelectList(db.RequestStages.Where(s=>allowed.Contains(s.Name)).OrderBy(s=>s.StageLevel).ToList(),"id","Name");
  }
  public ActionResult Index() { return View(db.Requests.Include(r=>r.Company).Include(r=>r.RequestStage).OrderByDescending(r=>r.DateRequested).ToList()); }
  public ActionResult Details(long? id) {
   var request=Find(id);if(request==null)return HttpNotFound();
   ViewBag.CanTransition=RequestWorkflow.NextStages(request.RequestStage==null?null:request.RequestStage.Name,User).Length>0;
   ViewBag.History=db.Database.SqlQuery<RequestHistoryEntry>("SELECT * FROM dbo.RequestHistory WHERE RequestId=@p0 ORDER BY Id",request.Id).ToList();
   return View(request);
  }
  [Authorize(Roles="ADMIN")]
  public ActionResult Create() { return new HttpStatusCodeResult(HttpStatusCode.Forbidden,"Submit resource requests through an organisation account."); }
  [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="ADMIN")]
  public ActionResult Create(Request request) { return new HttpStatusCodeResult(HttpStatusCode.Forbidden,"Submit resource requests through an organisation account."); }
  public ActionResult Edit(long? id) {
   var request=Find(id);if(request==null)return HttpNotFound();
   if(RequestWorkflow.NextStages(request.RequestStage==null?null:request.RequestStage.Name,User).Length==0)return new HttpStatusCodeResult(HttpStatusCode.Forbidden,"Your role has no action at the current request stage.");
   Populate(request);return View(request);
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Edit(long id,long? StageId,long? ExpectedStageId,string Note) {
   var request=Find(id);if(request==null)return HttpNotFound();
   if(!StageId.HasValue||!ExpectedStageId.HasValue)ModelState.AddModelError("","Choose the next stage and reload the request if its current stage is missing.");
   if((Note??"").Length>1000)ModelState.AddModelError("Note","Keep your note within 1,000 characters.");
   if(ModelState.IsValid) {
    try {
     RequestWorkflow.Transition(db.Database.Connection.ConnectionString,id,ExpectedStageId.Value,StageId.Value,User,Note);
     TempData["Success"]="Request updated. Its stock allocation and audit history were saved together.";
     return RedirectToAction("Details",new{id=id});
    } catch(UnauthorizedAccessException) { return new HttpStatusCodeResult(HttpStatusCode.Forbidden,"Your role cannot make that stage transition."); }
      catch(InvalidOperationException ex) { ModelState.AddModelError("",ex.Message); }
      catch(System.Data.SqlClient.SqlException) { ModelState.AddModelError("","The update could not be saved. Reload the request and try again."); }
   }
   db.Entry(request).Reload();Populate(request);return View(request);
  }
  [Authorize(Roles="ADMIN")]
  public ActionResult Delete(long? id) {
   var request=Find(id);if(request==null)return HttpNotFound();
   if(request.RequestStage==null||request.RequestStage.Name!="INITIAL")return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Only initial requests can be deleted.");
   return View(request);
  }
  [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken,Authorize(Roles="ADMIN")]
  public ActionResult DeleteConfirmed(long id) {
   try {RequestWorkflow.DeleteInitial(db.Database.Connection.ConnectionString,id,null,User);return RedirectToAction("Index");}
   catch(InvalidOperationException ex){return new HttpStatusCodeResult(HttpStatusCode.Conflict,ex.Message);}
  }
  protected override void Dispose(bool disposing){if(disposing)db.Dispose();base.Dispose(disposing);}
 }
}
