using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using ResourceAllocationService.Models;

namespace ResourceAllocationService.Controllers
{
    [Authorize]
    public class CompaniesController : Controller
    {
        private ResourceDistrubitionDBEntities db = new ResourceDistrubitionDBEntities();

    

        // GET: Companies/Details/5
        public ActionResult Details(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) name = User.Identity.Name;
            if (name != User.Identity.Name) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            var account = db.AspNetUsers.FirstOrDefault(user => user.UserName == User.Identity.Name);
            Company company = account == null ? null : account.Company;
            if (company == null)
            {
                return HttpNotFound();
            }
            return View(company);
        }

      



        // GET: Companies/Edit/5
        public ActionResult Edit(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) name = User.Identity.Name;
            if (name != User.Identity.Name) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            var account = db.AspNetUsers.FirstOrDefault(user => user.UserName == User.Identity.Name);
            Company company = account == null ? null : account.Company;
            if (company == null)
            {
                return HttpNotFound();
            }
            return View(company);
        }
        string SaveFiles(HttpPostedFileBase file) { return ImageUploads.Save(file, Server.MapPath("~/Uploads")); }

        // POST: Companies/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit( Company company,HttpPostedFileBase file)
        {
            var account = db.AspNetUsers.FirstOrDefault(user => user.UserName == User.Identity.Name);
            if (account == null || account.CompanyId != company.Id) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            if(!ImageUploads.Valid(file)) ModelState.AddModelError("", "Choose a JPG, PNG or GIF image under 5 MB.");
            if (file != null && file.ContentLength > 0) {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (file.ContentLength > 5 * 1024 * 1024 || !new[] { ".jpg", ".jpeg", ".png", ".gif" }.Contains(extension)) ModelState.AddModelError("", "Choose a JPG, PNG or GIF image under 5 MB.");
            }
            if (ModelState.IsValid)
            {
                
                if(file != null && file.ContentLength > 0)
                    company.PicUrl=SaveFiles(file);
                else company.PicUrl = account.Company.PicUrl;
                db.Entry(account.Company).CurrentValues.SetValues(company);
                db.SaveChanges();
                return RedirectToAction("Index","Manage");
            }
            return View(company);
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




