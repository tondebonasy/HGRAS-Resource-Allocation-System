using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ResourceAllocationAdmin.Models;

namespace ResourceAllocationAdmin.Controllers
{
    [Authorize(Roles = "ADMIN,APPROVER,RELEASER,DISPATCHER")]
    public class ResourcesController : Controller
    {
        private ResourceDistrubitionDBEntities db = new ResourceDistrubitionDBEntities();

        // GET: Resources
        public ActionResult Index()
        {
            return View(db.Resources.ToList());
        }

        // GET: Resources/Details/5
        public ActionResult Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Resource resource = db.Resources.Find(id);
            if (resource == null)
            {
                return HttpNotFound();
            }
            return View(resource);
        }

        // GET: Resources/Create
        [Authorize(Roles = "ADMIN")]
        public ActionResult Create()
        {
            return View();
        }

        // POST: Resources/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN")]
        public ActionResult Create([Bind(Include = "id,Name,Description,Quantity,PicUrl")] Resource resource)
        {
            if (ModelState.IsValid)
            {
                resource.PicUrl = "~/Content/img/logo.png";
                db.Resources.Add(resource);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(resource);
        }

        // GET: Resources/Edit/5
        [Authorize(Roles = "ADMIN")]
        public ActionResult Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Resource resource = db.Resources.Find(id);
            if (resource == null)
            {
                return HttpNotFound();
            }
            return View(resource);
        }

        // POST: Resources/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN")]
        public ActionResult Edit([Bind(Include = "id,Name,Description,Quantity,PicUrl")] Resource resource, long? ExpectedQuantity)
        {
            if (ModelState.IsValid)
            {
                var changed = db.Database.ExecuteSqlCommand("UPDATE dbo.Resource SET Name=@p0,Description=@p1,Quantity=@p2 WHERE id=@p3 AND (Quantity=@p4 OR (Quantity IS NULL AND @p4 IS NULL))",resource.Name,resource.Description,resource.Quantity,resource.id,ExpectedQuantity);
                if(changed != 1) { ModelState.AddModelError("", "Stock changed while you were editing. Reload the page before adjusting it.");return View(resource); }
                return RedirectToAction("Index");
            }
            return View(resource);
        }

        // GET: Resources/Delete/5
        [Authorize(Roles = "ADMIN")]
        public ActionResult Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Resource resource = db.Resources.Find(id);
            if (resource == null)
            {
                return HttpNotFound();
            }
            return View(resource);
        }

        // POST: Resources/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN")]
        public ActionResult DeleteConfirmed(long id)
        {
            Resource resource = db.Resources.Find(id);
            if(resource==null)return HttpNotFound();
            if(db.ResourceRequests.Any(r=>r.resourceId==id))return new HttpStatusCodeResult(HttpStatusCode.Conflict,"This resource is part of a request and cannot be deleted.");
            db.Resources.Remove(resource);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}


