using System.Collections.Generic;
namespace ResourceAllocationAdmin.Models
{
    public sealed class StageSummary
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }
    public sealed class OperationsOverview
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Active { get; set; }
        public int SubmittedToday { get; set; }
        public int Organisations { get; set; }
        public int Resources { get; set; }
        public int LowStock { get; set; }
        public List<Request> Recent { get; set; }
        public List<StageSummary> Stages { get; set; }
    }
}
