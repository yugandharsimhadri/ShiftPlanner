using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Services;

namespace ShiftPlanner.Api.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/export").RequireAuthorization();

        group.MapGet("/excel", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var (employees, entries, days) = await LoadMonthData(db, teamId, year, month);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add($"{year}-{month:D2}");

            ws.Cell(1, 1).Value = "Employee Code";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(1, 3).Value = "Track";
            for (var i = 0; i < days.Count; i++)
                ws.Cell(1, 4 + i).Value = days[i].ToString("dd");

            var row = 2;
            foreach (var emp in employees)
            {
                ws.Cell(row, 1).Value = emp.Code;
                ws.Cell(row, 2).Value = emp.Name;
                ws.Cell(row, 3).Value = emp.Track?.Name ?? "";
                for (var i = 0; i < days.Count; i++)
                {
                    var shiftCode = entries.FirstOrDefault(e => e.EmployeeId == emp.Id && e.Date == days[i])?.ShiftCode ?? "";
                    ws.Cell(row, 4 + i).Value = shiftCode;
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            var bytes = stream.ToArray();

            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"roster-{year}-{month:D2}.xlsx");
        }).RequireTeamMember();

        group.MapGet("/csv", async (int year, int month, AppDbContext db, HttpContext http) =>
        {
            var teamId = http.GetTeamContext().TeamId;
            var (employees, entries, days) = await LoadMonthData(db, teamId, year, month);

            var sb = new StringBuilder();
            sb.Append("Employee Code,Name,Track");
            foreach (var d in days) sb.Append(',').Append(d.ToString("dd"));
            sb.AppendLine();

            foreach (var emp in employees)
            {
                sb.Append(CsvEscape(emp.Code)).Append(',')
                  .Append(CsvEscape(emp.Name)).Append(',')
                  .Append(CsvEscape(emp.Track?.Name ?? ""));
                foreach (var d in days)
                {
                    var shiftCode = entries.FirstOrDefault(e => e.EmployeeId == emp.Id && e.Date == d)?.ShiftCode ?? "";
                    sb.Append(',').Append(CsvEscape(shiftCode));
                }
                sb.AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/csv", $"roster-{year}-{month:D2}.csv");
        }).RequireTeamMember();
    }

    private static async Task<(List<Models.Employee> employees, List<Models.RosterEntry> entries, List<DateOnly> days)> LoadMonthData(AppDbContext db, int teamId, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var employees = await db.Employees.Where(e => e.TeamId == teamId).Include(e => e.Track).OrderBy(e => e.Name).ToListAsync();
        var entries = await db.RosterEntries.Where(r => r.TeamId == teamId && r.Date >= start && r.Date <= end).ToListAsync();

        var days = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1)) days.Add(d);

        return (employees, entries, days);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
