using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ResourceAllocationService.Models;
using Pgh.Workflow;
namespace ResourceAllocationService.Controllers
{
 [Authorize]
 public class ResourceRequestsController:Controller
 {
  private readonly ResourceDistrubitionDBEntities db=new ResourceDistrubitionDBEntities();
  private AspNetUser CurrentUser(){return db.AspNetUsers.FirstOrDefault(u=>u.UserName==User.Identity.Name);}
  private Request Owned(long? id){var user=CurrentUser();return user==null||user.CompanyId==null?null:db.Requests.FirstOrDefault(r=>r.Id==id&&r.CompanyId==user.CompanyId);}
  private bool Initial(Request request){return request.RequestStage!=null&&request.RequestStage.Name=="INITIAL";}
  private void Populate(Request request){ViewBag.Resources=db.Resources.OrderBy(r=>r.Name).ToList();}
  private List<ResourceRequest> ReadResources(FormCollection form){
   var items=new List<ResourceRequest>();
   foreach(var resource in db.Resources.ToList()){
    if(form[resource.id.ToString()]==null)continue;
    int quantity;
    if(!Int32.TryParse(form[resource.id+"_Count"],out quantity)||quantity<1)ModelState.AddModelError("","Enter a positive whole-number quantity for "+resource.Name+".");
    else items.Add(new ResourceRequest{resourceId=resource.id,Count=quantity});
   }
   if(items.Count==0)ModelState.AddModelError("","Select at least one resource.");return items;
  }
  public ActionResult Details(long? id){
   var request=Owned(id);if(request==null)return HttpNotFound();
   ViewBag.History=db.Database.SqlQuery<RequestHistoryEntry>("SELECT * FROM dbo.RequestHistory WHERE RequestId=@p0 ORDER BY Id",request.Id).ToList();return View(request);
  }
  public ActionResult Create(){
   var user=CurrentUser();if(user==null||user.CompanyId==null)return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
   var request=new Request();Populate(request);return View(request);
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Create([Bind(Include="Description,additionalInfo")]Request request,FormCollection formCollection){
   var user=CurrentUser();if(user==null||user.CompanyId==null)return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
   var stage=db.RequestStages.FirstOrDefault(s=>s.Name=="INITIAL");
   if(stage==null)ModelState.AddModelError("","The initial stage is not configured. Contact an administrator.");
   var items=ReadResources(formCollection);
   request.CompanyId=user.CompanyId;request.RequestStage=stage;request.DateRequested=DateTime.Now;request.Requestor=user.Email;
   if(!ModelState.IsValid){Populate(request);return View(request);}
   using(var transaction=db.Database.BeginTransaction()){
    foreach(var item in items)request.ResourceRequests.Add(item);
    db.Requests.Add(request);db.SaveChanges();
    db.Database.ExecuteSqlCommand("INSERT dbo.RequestHistory(RequestId,ToStage,Actor,Note) VALUES(@p0,'INITIAL',@p1,'Resource request submitted.')",request.Id,User.Identity.Name);
    transaction.Commit();
   }
   return RedirectToAction("Details",new{id=request.Id});
  }
  public ActionResult Edit(long? id){var request=Owned(id);if(request==null)return HttpNotFound();if(!Initial(request))return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Only initial requests can be edited.");Populate(request);return View(request);}
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Edit([Bind(Include="Id,Description,additionalInfo")]Request input,FormCollection formCollection){
   var user=CurrentUser();var request=Owned(input.Id);if(request==null||user==null||!user.CompanyId.HasValue)return HttpNotFound();
   var selected=ReadResources(formCollection);
   if(ModelState.IsValid){
    try{RequestWorkflow.UpdateInitial(db.Database.Connection.ConnectionString,request.Id,user.CompanyId.Value,User.Identity.Name,input.Description,input.additionalInfo,selected.Select(i=>new RequestedItem{ResourceId=i.resourceId,Quantity=i.Count}).ToList());return RedirectToAction("Details",new{id=request.Id});}
    catch(UnauthorizedAccessException){return new HttpStatusCodeResult(HttpStatusCode.Forbidden);}
    catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
   }
   request.Description=input.Description;request.additionalInfo=input.additionalInfo;Populate(request);return View(request);
  }
  public ActionResult Delete(long? id){var request=Owned(id);if(request==null)return HttpNotFound();if(!Initial(request))return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Only initial requests can be deleted.");return View(request);}
  [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken]
  public ActionResult DeleteConfirmed(long? id){
   var request=Owned(id);var user=CurrentUser();if(request==null||user==null||!user.CompanyId.HasValue)return HttpNotFound();
   try{RequestWorkflow.DeleteInitial(db.Database.Connection.ConnectionString,request.Id,user.CompanyId.Value,User);return RedirectToAction("Details","Companies",new{name=User.Identity.Name});}
   catch(InvalidOperationException ex){return new HttpStatusCodeResult(HttpStatusCode.Conflict,ex.Message);}
  }
  protected override void Dispose(bool disposing){if(disposing)db.Dispose();base.Dispose(disposing);}
 }
}
