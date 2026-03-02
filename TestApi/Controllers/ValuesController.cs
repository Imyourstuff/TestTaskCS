using Microsoft.AspNetCore.Mvc;
using TestApi.Models;
using TestApi.Services;

[ApiController]
[Route("api")]
public class ValuesController : ControllerBase
{
    private readonly CSVProcessor _csvProcessor;
    private readonly IDataService _dataService;
    private readonly ILogger<ValuesController> _logger;

    public ValuesController(CSVProcessor csvProcessor, IDataService dataService, ILogger<ValuesController> logger)
    {
        _csvProcessor = csvProcessor;
        _dataService = dataService;
        _logger = logger;
    }

    // Страшные штуки для Swagger...
    [HttpPost("upload")]
    [ProducesResponseType(typeof(List<ValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не выбран или пуст");

        var fileName = Path.GetFileName(file.FileName);

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _csvProcessor.ImportAsync(
                fileName,
                stream,
                HttpContext.RequestAborted);

            return Ok(new
            {
                Message = "Файл успешно обработан.",
                ImportedRows = result.RowCount,
                Aggregates = new
                {
                    result.MinDate,
                    result.MaxDate,
                    result.AvgExecutionTime,
                    result.AvgValue,
                    result.MinValue,
                    result.MaxValue,
                    result.MedianValue
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке файла {FileName}", fileName);
            return BadRequest(new { Error = ex.Message });
        }
    }


    [HttpGet("results")]
    [ProducesResponseType(typeof(List<ValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ResultDto>>> GetResults([FromQuery] ResultFilter filter)
    {
        var results = await _dataService.GetResultsAsync(filter);
        return Ok(results);
    }

    [HttpGet("values/last10")]
    [ProducesResponseType(typeof(List<ValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ValueDto>>> GetLast10Values([FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("Не указано имя файла");

        var values = await _dataService.GetLast10ValuesAsync(fileName);
        return Ok(values);
    }
}