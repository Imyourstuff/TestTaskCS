namespace TestApi.Models
{
    public class CSVImportResult
    {
        public int RowCount { get; set; }
        public DateTime MinDate { get; set; }
        public DateTime MaxDate { get; set; }
        public double AvgExecutionTime { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double MedianValue { get; set; }

        public double DeltaSeconds =>
            (MaxDate - MinDate).TotalSeconds;
    }
}
