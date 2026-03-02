namespace TestApi.Services
{
    using Microsoft.EntityFrameworkCore;
    using Npgsql;
    using System.Globalization;
    using TestApi.Models;

    public class CSVProcessor
    {
        private readonly AppDbContext _context;
        public CSVProcessor(AppDbContext context)
        {
            _context = context;
        }

        private NpgsqlConnection GetConnection()
        {
            return (NpgsqlConnection)_context.Database.GetDbConnection();
        }

        public async Task<CSVImportResult> ImportAsync(string fileName, Stream stream, CancellationToken ct)
        {
            var connection = GetConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(ct);

            // Транзакция будет автоматически откачена, если Commit не будет вызван
            await using var transaction = await connection.BeginTransactionAsync(ct);

            // Удаляем старые записи для этого файла (перезапись)
            await using (var deleteCmd = new NpgsqlCommand(
                @"DELETE FROM ""Values"" WHERE ""FileName"" = @fileName",
                connection, transaction))
            {
                deleteCmd.Parameters.AddWithValue("@fileName", fileName);
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            using var reader = new StreamReader(stream);

            // Проверка заголовка
            var header = await reader.ReadLineAsync(ct);
            if (header is null)
                throw new Exception("Файл пуст.");
            if (header.Trim() != "Date;ExecutionTime;Value")
                throw new Exception("Неверный формат заголовка");

            int rowCount = 0;
            DateTime? minDate = null;
            DateTime? maxDate = null;
            double sumExec = 0;
            double sumValue = 0;
            double minValue = double.MaxValue;
            double maxValue = double.MinValue;
            var valuesForMedian = new List<double>();

            {
                await using var writer = await connection.BeginBinaryImportAsync(
                    """
                COPY "Values" ("FileName", "Date", "ExecutionTime", "Value")
                FROM STDIN (FORMAT BINARY)
                """,
                    ct);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(ct);
                    rowCount++;

                    if (rowCount > 10000)
                        throw new Exception("Количество строк превышает 10000");

                    if (string.IsNullOrWhiteSpace(line))
                        throw new Exception($"Пустая строка {rowCount}");

                    var parts = line.Split(';');
                    if (parts.Length != 3)
                        throw new Exception($"В строке {rowCount} не 3 значения");

                    if (!DateTime.TryParseExact(parts[0],
                        "yyyy-MM-ddTHH-mm-ss.ffffZ",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var date))
                        throw new Exception($"Неверная дата в строке {rowCount}");

                    if (!double.TryParse(parts[1],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var executionTime))
                        throw new Exception($"Неверный ExecutionTime в строке {rowCount}");

                    if (!double.TryParse(parts[2],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var value))
                        throw new Exception($"Неверный Value в строке {rowCount}");

                    if (date < new DateTime(2000, 1, 1) || date > DateTime.UtcNow)
                        throw new Exception($"Дата вне диапазона в строке {rowCount}");

                    if (executionTime < 0)
                        throw new Exception($"ExecutionTime < 0 в строке {rowCount}");

                    if (value < 0)
                        throw new Exception($"Value < 0 в строке {rowCount}");

                    await writer.StartRowAsync(ct);
                    await writer.WriteAsync(fileName, ct);
                    await writer.WriteAsync(date, ct);
                    await writer.WriteAsync(executionTime, ct);
                    await writer.WriteAsync(value, ct);

                    minDate ??= date;
                    if (date < minDate) minDate = date;
                    maxDate ??= date;
                    if (date > maxDate) maxDate = date;

                    sumExec += executionTime;
                    sumValue += value;
                    if (value < minValue) minValue = value;
                    if (value > maxValue) maxValue = value;

                    valuesForMedian.Add(value);
                }

                if (rowCount == 0)
                    throw new Exception("Нет данных");

                await writer.CompleteAsync(ct);
            }

            // Вычисление медианы
            valuesForMedian.Sort();
            double median = valuesForMedian.Count % 2 == 0
                ? (valuesForMedian[rowCount / 2 - 1] + valuesForMedian[rowCount / 2]) / 2
                : valuesForMedian[rowCount / 2];

            // Вставка или обновление агрегированного результата
            var upsertResultCmd = @"
                INSERT INTO ""Results"" (""FileName"", ""TimeDeltaSeconds"", ""FirstOperationStart"", ""AvgExecutionTime"", ""AvgValue"", ""MedianValue"", ""MaxValue"", ""MinValue"")
                VALUES (@fileName, @timeDelta, @firstStart, @avgExec, @avgValue, @median, @max, @min)
                ON CONFLICT (""FileName"") DO UPDATE SET
                ""TimeDeltaSeconds"" = EXCLUDED.""TimeDeltaSeconds"",
                ""FirstOperationStart"" = EXCLUDED.""FirstOperationStart"",
                ""AvgExecutionTime"" = EXCLUDED.""AvgExecutionTime"",
                ""AvgValue"" = EXCLUDED.""AvgValue"",
                ""MedianValue"" = EXCLUDED.""MedianValue"",
                ""MaxValue"" = EXCLUDED.""MaxValue"",
                ""MinValue"" = EXCLUDED.""MinValue"";";

            await using var cmd = new NpgsqlCommand(upsertResultCmd, connection, transaction);
            cmd.Parameters.AddWithValue("@fileName", fileName);
            cmd.Parameters.AddWithValue("@timeDelta", (maxDate!.Value - minDate!.Value).TotalSeconds);
            cmd.Parameters.AddWithValue("@firstStart", minDate.Value);
            cmd.Parameters.AddWithValue("@avgExec", sumExec / rowCount);
            cmd.Parameters.AddWithValue("@avgValue", sumValue / rowCount);
            cmd.Parameters.AddWithValue("@median", median);
            cmd.Parameters.AddWithValue("@max", maxValue);
            cmd.Parameters.AddWithValue("@min", minValue);
            await cmd.ExecuteNonQueryAsync(ct);

            // Фиксация транзакции
            await transaction.CommitAsync(ct);

            return new CSVImportResult
            {
                RowCount = rowCount,
                MinDate = minDate.Value,
                MaxDate = maxDate.Value,
                AvgExecutionTime = sumExec / rowCount,
                AvgValue = sumValue / rowCount,
                MinValue = minValue,
                MaxValue = maxValue,
                MedianValue = median
            };
        }
    }
}
