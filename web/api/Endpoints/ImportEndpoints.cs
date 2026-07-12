using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Dtos;
using ShiftPlanner.Api.Models;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class ImportEndpoints
{
    // Expected columns (header row required):
    // Name, Phone, Email, Track, Subtrack, Role, EmploymentType, JoinDate, Status, Notes
    private static readonly string[] ExpectedHeaders =
        { "Name", "Phone", "Email", "Track", "Subtrack", "Role", "EmploymentType", "JoinDate", "Status", "Notes" };

    public static void MapImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import").RequireAuthorization();

        group.MapPost("/employees", async (IFormFile file, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;

            if (file.Length == 0) return Results.BadRequest("Empty file.");

            List<Dictionary<string, string>> rows;
            try
            {
                rows = file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? ReadXlsx(file)
                    : ReadCsv(file);
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Could not parse file: {ex.Message}");
            }

            var tracks = await db.Tracks.Where(t => t.TeamId == teamId).ToListAsync();
            var subtracks = await db.Subtracks.Where(s => s.TeamId == teamId).ToListAsync();

            // Design choice: validate every row first; commit only the rows that pass validation
            // (row-level partial commit), and report the rest as per-row errors.
            var errors = new List<ImportRowError>();
            var toInsert = new List<Employee>();

            var rowNum = 1; // header is row 1
            foreach (var row in rows)
            {
                rowNum++;
                var name = row.GetValueOrDefault("Name", "").Trim();
                var phone = row.GetValueOrDefault("Phone", "").Trim();
                var trackName = row.GetValueOrDefault("Track", "").Trim();
                var subtrackName = row.GetValueOrDefault("Subtrack", "").Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new ImportRowError(rowNum, "Name is required."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(phone))
                {
                    errors.Add(new ImportRowError(rowNum, "Phone is required."));
                    continue;
                }

                var track = tracks.FirstOrDefault(t => string.Equals(t.Name, trackName, StringComparison.OrdinalIgnoreCase));
                if (track is null)
                {
                    errors.Add(new ImportRowError(rowNum, $"Track '{trackName}' not found."));
                    continue;
                }

                Subtrack? subtrack = null;
                if (!string.IsNullOrWhiteSpace(subtrackName))
                {
                    subtrack = subtracks.FirstOrDefault(s => s.TrackId == track.Id && string.Equals(s.Name, subtrackName, StringComparison.OrdinalIgnoreCase));
                    if (subtrack is null)
                    {
                        errors.Add(new ImportRowError(rowNum, $"Subtrack '{subtrackName}' not found under track '{trackName}'."));
                        continue;
                    }
                }

                var employmentType = EmploymentType.FullTime;
                var employmentTypeRaw = row.GetValueOrDefault("EmploymentType", "").Trim();
                if (!string.IsNullOrWhiteSpace(employmentTypeRaw) && !Enum.TryParse(employmentTypeRaw, true, out employmentType))
                {
                    errors.Add(new ImportRowError(rowNum, $"Invalid EmploymentType '{employmentTypeRaw}'. Use FullTime or PartTime."));
                    continue;
                }

                var status = EmployeeStatus.Active;
                var statusRaw = row.GetValueOrDefault("Status", "").Trim();
                if (!string.IsNullOrWhiteSpace(statusRaw) && !Enum.TryParse(statusRaw, true, out status))
                {
                    errors.Add(new ImportRowError(rowNum, $"Invalid Status '{statusRaw}'. Use Active or Inactive."));
                    continue;
                }

                var joinDate = DateOnly.FromDateTime(DateTime.Today);
                var joinDateRaw = row.GetValueOrDefault("JoinDate", "").Trim();
                if (!string.IsNullOrWhiteSpace(joinDateRaw))
                {
                    if (!DateOnly.TryParse(joinDateRaw, out joinDate))
                    {
                        errors.Add(new ImportRowError(rowNum, $"Invalid JoinDate '{joinDateRaw}'. Use YYYY-MM-DD."));
                        continue;
                    }
                }

                toInsert.Add(new Employee
                {
                    TeamId = teamId,
                    Name = name,
                    Phone = phone,
                    Email = string.IsNullOrWhiteSpace(row.GetValueOrDefault("Email", "")) ? null : row["Email"].Trim(),
                    TrackId = track.Id,
                    SubtrackId = subtrack?.Id,
                    Role = row.GetValueOrDefault("Role", "").Trim(),
                    EmploymentType = employmentType,
                    JoinDate = joinDate,
                    Status = status,
                    Notes = string.IsNullOrWhiteSpace(row.GetValueOrDefault("Notes", "")) ? null : row["Notes"].Trim()
                });
            }

            // Assign sequential codes for the valid rows, continuing after the current max.
            var nextCodeBase = await EmployeesEndpoints.SuggestNextEmployeeCode(db, teamId);
            var nextNum = int.Parse(nextCodeBase.Split('-')[1]);
            foreach (var emp in toInsert)
            {
                emp.Code = $"EMP-{nextNum:D3}";
                nextNum++;
                db.Employees.Add(emp);
            }

            if (toInsert.Count > 0)
                await db.SaveChangesAsync();

            return Results.Ok(new ImportResult(toInsert.Count, errors));
        }).RequireTeamEditor().DisableAntiforgery();
    }

    private static List<Dictionary<string, string>> ReadCsv(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null) lines.Add(line);

        if (lines.Count == 0) return new List<Dictionary<string, string>>();

        var headers = ParseCsvLine(lines[0]);
        var result = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = ParseCsvLine(lines[i]);
            var dict = new Dictionary<string, string>();
            for (var c = 0; c < headers.Count; c++)
                dict[headers[c]] = c < values.Count ? values[c] : "";
            result.Add(dict);
        }
        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { values.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        values.Add(current.ToString());
        return values;
    }

    private static List<Dictionary<string, string>> ReadXlsx(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var firstRow = ws.FirstRowUsed();
        if (firstRow is null) return new List<Dictionary<string, string>>();

        var headers = firstRow.Cells().Select(c => c.GetString().Trim()).ToList();
        var result = new List<Dictionary<string, string>>();

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>();
            for (var c = 0; c < headers.Count; c++)
                dict[headers[c]] = row.Cell(c + 1).GetString().Trim();
            result.Add(dict);
        }
        return result;
    }
}
