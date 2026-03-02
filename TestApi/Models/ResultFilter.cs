namespace TestApi.Models
{
    public class ResultFilter
    {
        public string? FileName { get; set; }
        public DateTime? FirstOperationStartFrom { get; set; }
        public DateTime? FirstOperationStartTo { get; set; }
        public double? AvgValueFrom { get; set; }
        public double? AvgValueTo { get; set; }
        public double? AvgExecutionTimeFrom { get; set; }
        public double? AvgExecutionTimeTo { get; set; }
    }
}
