using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace AnalysisServicesLib;

public record ColumnDto(string Name, string DataType, bool IsHidden);
public record MeasureDto(string Name, string Expression, bool IsHidden);
public record TableDto(string Name, bool IsHidden, IReadOnlyList<ColumnDto> Columns, IReadOnlyList<MeasureDto> Measures);
public class ModelSchema { public IReadOnlyList<TableDto> Tables { get; init; } = Array.Empty<TableDto>(); }

public class DatasetService(string xmlaEndpoint, AccessToken accessToken, string datasetId, string? datasetName = null)
{
    private string InitialCatalog => string.IsNullOrWhiteSpace(datasetName) ? datasetId : datasetName;

    public IEnumerable<(string Id, string Name)> ListDatabases()
    {
        if (!xmlaEndpoint.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid XMLA endpoint: '{xmlaEndpoint}'. Expected format 'powerbi://api.powerbi.com/v1.0/myorg/<workspace-name>'.");

        var server = new Microsoft.AnalysisServices.Tabular.Server { AccessToken = accessToken };
        server.Connect(xmlaEndpoint);
        try
        {
            foreach (Microsoft.AnalysisServices.Tabular.Database db in server.Databases)
                yield return (db.ID, db.Name);
        }
        finally
        {
            server.Disconnect();
        }
    }

    // Extract tables, columns, and measures via XMLA (TOM)
    public ModelSchema GetModelSchema()
    {
        if (!xmlaEndpoint.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid XMLA endpoint: '{xmlaEndpoint}'. Expected format 'powerbi://api.powerbi.com/v1.0/myorg/<workspace-name>'.");

        var server = new Microsoft.AnalysisServices.Tabular.Server { AccessToken = accessToken };
        server.Connect(xmlaEndpoint);
        try
        {
            var db = server.Databases
                .Cast<Microsoft.AnalysisServices.Tabular.Database>()
                .FirstOrDefault(d =>
                    d.ID.Equals(datasetId, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(datasetName) && d.Name.Equals(datasetName, StringComparison.OrdinalIgnoreCase)));

            if (db is null)
                throw new InvalidOperationException($"Dataset not found at XMLA endpoint. Endpoint='{xmlaEndpoint}', DatasetId='{datasetId}', DatasetName='{datasetName}'.");

            var model = db.Model;

            var tables = model.Tables.Select(t =>
                new TableDto(
                    t.Name,
                    t.IsHidden,
                    t.Columns.Select(c => new ColumnDto(c.Name, c.DataType.ToString(), c.IsHidden)).ToList(),
                    t.Measures.Select(m => new MeasureDto(m.Name, m.Expression, m.IsHidden)).ToList()
                )).ToList();

            return new ModelSchema { Tables = tables };
        }
        finally
        {
            server.Disconnect();
        }
    }

    // Run a DAX query (returns DataTable - do NOT serialize this directly)
    public DataTable ExecuteDax(string dax)
    {
        if (!xmlaEndpoint.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid XMLA endpoint: '{xmlaEndpoint}'. Expected format 'powerbi://api.powerbi.com/v1.0/myorg/<workspace-name>'.");

        using var conn = new AdomdConnection($"Data Source={xmlaEndpoint};Initial Catalog={InitialCatalog};");
        conn.AccessToken = accessToken;
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = dax;

        using var adapter = new AdomdDataAdapter(cmd);
        var dt = new DataTable();
        adapter.Fill(dt);
        return dt;
    }

    // Safe: execute DAX and return plain rows (safe to serialize with System.Text.Json)
    public IReadOnlyList<Dictionary<string, object?>> ExecuteDaxRows(string dax)
    {
        var dt = ExecuteDax(dax);
        return ToRows(dt);
    }

    // Convenience: execute DAX and return JSON (no DataTable metadata)
    public string ExecuteDaxAsJson(string dax, JsonSerializerOptions? options = null)
    {
        var rows = ExecuteDaxRows(dax);
        return System.Text.Json.JsonSerializer.Serialize(rows, options ?? new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<Dictionary<string, object?>> ToRows(DataTable dt)
    {
        var cols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
        var list = new List<Dictionary<string, object?>>(dt.Rows.Count);

        foreach (DataRow row in dt.Rows)
        {
            var dict = new Dictionary<string, object?>(cols.Length, StringComparer.Ordinal);
            foreach (var col in cols)
            {
                var val = row[col];
                dict[col] = val == DBNull.Value ? null : Normalize(val);
            }
            list.Add(dict);
        }

        return list;
    }

    private static object? Normalize(object value)
    {
        // Keep common primitives as-is; convert types that serialize poorly
        return value switch
        {
            System.DBNull => null,
            byte[] b => Convert.ToBase64String(b),
            _ => value
        };
    }
}
