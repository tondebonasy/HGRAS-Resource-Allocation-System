using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Hgras.Network;

namespace Hgras.Web
{
    [Authorize]
    public sealed class NetworkController : Controller
    {
        private NetworkService Service { get { return new NetworkService(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString,User.Identity.GetUserId(),User.Identity.Name); } }
        public ActionResult Index(long? groupId)
        {
            try {
                HgrasSchema.Ensure(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString,Server.MapPath("~/App_Data/003-hgras-network.sql"));
                return View(Service.Load(groupId));
            }
            catch(UnauthorizedAccessException) { return new HttpStatusCodeResult(403); }
            catch(SqlException ex) {
                System.Diagnostics.Trace.TraceError("HGRAS database error {0}: {1}",ex.Number,ex.Message);
                Response.StatusCode=503;
                Response.TrySkipIisCustomErrors=true;
                ViewBag.NetworkUnavailable=true;
                return View(new NetworkWorkspace {Groups=new DataTable()});
            }
        }
        private ActionResult Change(long groupId,Action action)
        {
            var actionName=Convert.ToString(RouteData.Values["action"]);
            var tab=actionName=="AddFacility"||actionName=="AddDepartment"?"facilities":actionName=="AssignMember"?"access":actionName=="AddStock"||actionName=="ReconcileStock"?"inventory":actionName=="SaveOperation"?(Request.Form["kind"]??"overview").ToLowerInvariant():actionName=="Acknowledge"?"notifications":"transfers";
            if(!ModelState.IsValid){TempData["NetworkError"]="Check all required fields, dates and numeric values.";return RedirectToAction("Index",new{groupId,tab});}
            try {action();TempData["NetworkSuccess"]="Saved. The network workspace shows the latest recorded data.";}
            catch(UnauthorizedAccessException) {return new HttpStatusCodeResult(403);}
            catch(InvalidOperationException ex) {TempData["NetworkError"]=ex.Message;}
            catch(SqlException ex) when(ex.Number==1205 || ex.Number==8115 || ex.Number==2601 || ex.Number==2627) {TempData["NetworkError"]="The update conflicted with another change or exceeded a valid quantity. Nothing was saved; refresh and retry.";}
            return RedirectToAction("Index",new{groupId,tab});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult CreateGroup(string name)
        {
            if(!User.IsInRole("ADMIN")) return new HttpStatusCodeResult(403);
            try {return RedirectToAction("Index",new{groupId=Service.CreateGroup(name)});}
            catch(InvalidOperationException ex) {TempData["NetworkError"]=ex.Message;return RedirectToAction("Index");}
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Acknowledge(long groupId,long noticeId)
        {return Change(groupId,()=>Service.Acknowledge(groupId,noticeId));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult AddFacility(long groupId,string name,string location,string officer)
        {return Change(groupId,()=>Service.AddFacility(groupId,name,location,officer));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult AddDepartment(long groupId,long facilityId,string name,string officer)
        {return Change(groupId,()=>Service.AddDepartment(groupId,facilityId,name,officer));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult AssignMember(long groupId,string email,long? facilityId,string role)
        {return Change(groupId,()=>Service.AssignMember(groupId,email,facilityId,role));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult AddStock(long groupId,long facilityId,string name,string category,string unit,string batch,DateTime? expires,string handling,long quantity,long minimum,long reorder)
        {if(!ModelState.IsValid){TempData["NetworkError"]="Check dates and numeric fields.";return RedirectToAction("Index",new{groupId});}return Change(groupId,()=>Service.AddStock(groupId,facilityId,name,category,unit,batch,expires,handling,quantity,minimum,reorder));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult RequestTransfer(long groupId,long stockId,long destination,long quantity,string priority,string reason)
        {return Change(groupId,()=>Service.RequestTransfer(groupId,stockId,destination,quantity,priority,reason));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Transition(long groupId,long transferId,string expected,string target,string note,bool suitabilityReviewed=false,long? receivedQuantity=null)
        {if(target=="APPROVED"&&!suitabilityReviewed){TempData["NetworkError"]="An authorised officer must confirm suitability, storage and delivery feasibility before approval.";return RedirectToAction("Index",new{groupId});}return Change(groupId,()=>Service.Transition(groupId,transferId,expected,target,note,receivedQuantity));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult ReconcileStock(long groupId,long stockId,long expected,long quantity,long minimum,long reorder,string note)
        {return Change(groupId,()=>Service.ReconcileStock(groupId,stockId,expected,quantity,minimum,reorder,note));}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult SaveOperation(long groupId,long facilityId,long? id,string kind,string name,string status,int total,int available,DateTime? due,string notes)
        {if(!ModelState.IsValid){TempData["NetworkError"]="Check the operational record fields.";return RedirectToAction("Index",new{groupId});}return Change(groupId,()=>Service.SaveOperation(groupId,facilityId,id,kind,name,status,total,available,due,notes));}
        public ActionResult Report(long groupId)
        {
            try {
                var model=Service.Load(groupId); var csv=new StringBuilder("Resource,Hospital,Unit,On hand,Reserved,Minimum reserve,Shareable,Last recorded UTC\r\n");
                foreach(DataRow row in model.Stock.Rows){var fields=new[]{"Name","FacilityName","Unit","Quantity","Reserved","MinimumReserve","Shareable","UpdatedAtUtc"};for(var i=0;i<fields.Length;i++){if(i>0)csv.Append(',');var value=Convert.ToString(row[fields[i]],System.Globalization.CultureInfo.InvariantCulture);if(value.Length>0&&"=+-@\t\r".IndexOf(value[0])>=0)value="'"+value;csv.Append('"').Append(value.Replace("\"","\"\"")).Append('"');}csv.Append("\r\n");}
                return File(Encoding.UTF8.GetBytes(csv.ToString()),"text/csv","hgras-stock-report.csv");
            }catch(UnauthorizedAccessException){return new HttpStatusCodeResult(403);}
        }
    }
}
