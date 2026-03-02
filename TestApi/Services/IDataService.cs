using TestApi.Models;

public interface IDataService
{

    Task<List<ResultDto>> GetResultsAsync(ResultFilter filter);
    Task<List<ValueDto>> GetLast10ValuesAsync(string fileName);
}

