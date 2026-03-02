using TestApi.Models;
using Microsoft.EntityFrameworkCore;

public class DataService : IDataService
{
    private readonly AppDbContext _context;

    public DataService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ResultDto>> GetResultsAsync(ResultFilter filter)
    {
        var query = _context.Results.AsNoTracking();

        // фильтрация
        if (!string.IsNullOrEmpty(filter.FileName))
            query = query.Where(r => r.FileName == filter.FileName);

        if (filter.FirstOperationStartFrom.HasValue)
            query = query.Where(r => r.FirstOperationStart >= filter.FirstOperationStartFrom.Value);

        if (filter.FirstOperationStartTo.HasValue)
            query = query.Where(r => r.FirstOperationStart <= filter.FirstOperationStartTo.Value);

        if (filter.AvgValueFrom.HasValue)
            query = query.Where(r => r.AvgValue >= filter.AvgValueFrom.Value);

        if (filter.AvgValueTo.HasValue)
            query = query.Where(r => r.AvgValue <= filter.AvgValueTo.Value);

        if (filter.AvgExecutionTimeFrom.HasValue)
            query = query.Where(r => r.AvgExecutionTime >= filter.AvgExecutionTimeFrom.Value);

        if (filter.AvgExecutionTimeTo.HasValue)
            query = query.Where(r => r.AvgExecutionTime <= filter.AvgExecutionTimeTo.Value);

        return await query
            .Select(r => new ResultDto
            {
                FileName = r.FileName,
                TimeDeltaSeconds = r.TimeDeltaSeconds,
                FirstOperationStart = r.FirstOperationStart,
                AvgExecutionTime = r.AvgExecutionTime,
                AvgValue = r.AvgValue,
                MedianValue = r.MedianValue,
                MaxValue = r.MaxValue,
                MinValue = r.MinValue
            })
            .ToListAsync();
    }

    public async Task<List<ValueDto>> GetLast10ValuesAsync(string fileName)
    {
        return await _context.Values
            .AsNoTracking()
            .Where(v => v.FileName == fileName)
            .OrderByDescending(v => v.Date)
            .Take(10)
            .Select(v => new ValueDto
            {
                Date = v.Date,
                ExecutionTime = v.ExecutionTime,
                Value = v.Value
            })
            .ToListAsync();
    }
}