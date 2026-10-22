using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ResourceAllocationAdmin.Models;
namespace ResourceAllocationAdmin.Controllers {
 [Authorize(Roles="ADMIN")]
 public class AspNetUsersController:Controller {
  private readonly ResourceDistrubitionDBEntities db=new ResourceDistrubitionDBEntities();
  public ActionResult Index(bool? hasrole=false){var users=db.AspNetUsers.Include(u=>u.Company).Include(u=>u.AspNetRoles);return View(users.Where(u=>hasrole==true?u.CompanyId==null:u.CompanyId!=null).ToList());}
  public ActionResult Details(string id){var user=db.AspNetUsers.Find(id);if(user==null)return HttpNotFound();return View(user);}
  public ActionResult Create(){return RedirectToAction("Register","Account");}
  public ActionResult Edit(string id){var user=db.AspNetUsers.Find(id);if(user==null)return HttpNotFound();ViewBag.Roles=db.AspNetRoles.ToList();return View(user);}
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Edit(string id,string PhoneNumber,string[] SelectedRoles,bool Enabled=true){
   var user=db.AspNetUsers.Find(id);if(user==null)return HttpNotFound();
   var selected=(SelectedRoles??new string[0]).Distinct().ToArray();
   var roles=db.AspNetRoles.Where(r=>selected.Contains(r.Id)).ToList();
   if(roles.Count!=selected.Length)ModelState.AddModelError("","Choose valid system roles.");
   if(user.CompanyId.HasValue&&roles.Any())ModelState.AddModelError("","Organisation accounts cannot receive staff roles.");
   if((PhoneNumber??"").Length>50)ModelState.AddModelError("PhoneNumber","Phone numbers must be within 50 characters.");
   if(user.AspNetRoles.Any(r=>r.Name=="ADMIN")&&!roles.Any(r=>r.Name=="ADMIN")&&db.AspNetRoles.First(r=>r.Name=="ADMIN").AspNetUsers.Count<=1)ModelState.AddModelError("","Keep at least one administrator account.");
   if(!Enabled&&user.UserName==User.Identity.Name)ModelState.AddModelError("","You cannot disable your own account.");
   if(!Enabled&&user.AspNetRoles.Any(r=>r.Name=="ADMIN")&&db.AspNetRoles.First(r=>r.Name=="ADMIN").AspNetUsers.Count<=1)ModelState.AddModelError("","Keep at least one administrator enabled.");
   if(ModelState.IsValid){user.LockoutEnabled=true;user.LockoutEndDateUtc=Enabled?(DateTime?)null:DateTime.UtcNow.AddYears(50);user.PhoneNumber=PhoneNumber;user.AspNetRoles.Clear();foreach(var role in roles)user.AspNetRoles.Add(role);user.SecurityStamp=Guid.NewGuid().ToString();db.SaveChanges();return RedirectToAction("Details",new{id=user.Id});}
   ViewBag.Roles=db.AspNetRoles.ToList();return View(user);
  }
  public ActionResult Delete(string id){var user=db.AspNetUsers.Find(id);if(user==null)return HttpNotFound();return View(user);}
  [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken]
  public ActionResult Disable(string id){
   var user=db.AspNetUsers.Find(id);if(user==null)return HttpNotFound();
   if(user.UserName==User.Identity.Name)return new HttpStatusCodeResult(HttpStatusCode.Conflict,"You cannot disable your own account.");
   if(user.AspNetRoles.Any(r=>r.Name=="ADMIN")&&db.AspNetRoles.First(r=>r.Name=="ADMIN").AspNetUsers.Count<=1)return new HttpStatusCodeResult(HttpStatusCode.Conflict,"Keep at least one administrator.");
   user.LockoutEnabled=true;user.LockoutEndDateUtc=DateTime.UtcNow.AddYears(50);user.SecurityStamp=Guid.NewGuid().ToString();db.SaveChanges();return RedirectToAction("Index",new{hasrole=!user.CompanyId.HasValue});
  }
  protected override void Dispose(bool disposing){if(disposing)db.Dispose();base.Dispose(disposing);}
 }
}

