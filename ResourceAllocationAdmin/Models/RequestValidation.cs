using System.ComponentModel.DataAnnotations;
namespace ResourceAllocationAdmin.Models {
 [MetadataType(typeof(RequestRules))] public partial class Request {}
 public class RequestRules {
  [Required,StringLength(500)] public string Description {get;set;}
  [StringLength(250)] public string additionalInfo {get;set;}
 }
 [MetadataType(typeof(ResourceRules))] public partial class Resource {}
 public class ResourceRules {
  [Required,StringLength(50)] public string Name {get;set;}
  [StringLength(225)] public string Description {get;set;}
  [Required,Range(typeof(long),"0","9223372036854775807")] public long? Quantity {get;set;}
 } [MetadataType(typeof(CompanyRules))] public partial class Company {}
 public class CompanyRules {
  [Required,StringLength(50)] public string Name {get;set;}
  [Required,EmailAddress,StringLength(50)] public string Email {get;set;}
  [StringLength(200)] public string Address {get;set;}
  [StringLength(50)] public string PhoneNumber {get;set;}
  [StringLength(50)] public string GroupHead {get;set;}
  [StringLength(50)] public string RegNumber {get;set;}
 }}


