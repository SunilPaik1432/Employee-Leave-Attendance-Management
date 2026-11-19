using System.Data;
using System.Text.RegularExpressions;
using System.Data.SqlClient;

public record Grid(string Title, IReadOnlyList<Dictionary<string,object?>> Rows);
public record Dashboard(IReadOnlyList<Grid> Grids);

public class SqlStore
{
    private readonly string connection;
    public SqlStore(IConfiguration config) => connection = config.GetConnectionString("Default") ??
        @"Server=(localdb)\MSSQLLocalDB;Database=EmployeePortal;Integrated Security=true;TrustServerCertificate=true";

    public async Task Initialize()
    {
        var settings = new SqlConnectionStringBuilder(connection);
        var name = settings.InitialCatalog;
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Specify a database in the connection string.");
        settings.InitialCatalog = "master";
        await using (var master = new SqlConnection(settings.ConnectionString))
        {
            await master.OpenAsync();
            using var create = new SqlCommand("IF DB_ID(@name) IS NULL BEGIN DECLARE @sql nvarchar(300)=N'CREATE DATABASE '+QUOTENAME(@name); EXEC(@sql); END;",master);
            create.Parameters.Add("@name",SqlDbType.NVarChar,128).Value = name;
            await create.ExecuteNonQueryAsync();
        }
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"schema.sql"));
        await using var db = new SqlConnection(connection);
        await db.OpenAsync();
        using var tx = db.BeginTransaction();
        using var marker = new SqlCommand("SELECT OBJECT_ID(N'dbo.Staff',N'U');",db,tx);
        if (await marker.ExecuteScalarAsync() is DBNull)
            foreach (var batch in Regex.Split(schema,@"^\s*GO\s*$",RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var command = new SqlCommand(batch,db,tx);
                await command.ExecuteNonQueryAsync();
            }
        await tx.CommitAsync();
    }
    public Task Run(string sql,params (string Name,object? Value)[] values) => Execute(sql,CommandType.Text,values);
    public Task Procedure(string sql,params (string Name,object? Value)[] values) => Execute(sql,CommandType.StoredProcedure,values);

    private async Task Execute(string sql,CommandType type,(string Name,object? Value)[] values)
    {
        await using var db = new SqlConnection(connection);
        await db.OpenAsync();
        using var command = Prepare(db,sql,values);
        command.CommandType = type;
        await command.ExecuteNonQueryAsync();
    }
    public async Task<IReadOnlyList<Dictionary<string,object?>>> Read(string sql,params (string Name,object? Value)[] values)
    {
        await using var db = new SqlConnection(connection);
        await db.OpenAsync();
        using var command = Prepare(db,sql,values);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string,object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string,object?>();
            for (var i=0;i<reader.FieldCount;i++) row.Add(reader.GetName(i),reader.IsDBNull(i) ? null : reader.GetValue(i));
            rows.Add(row);
        }
        return rows;
    }
    private static SqlCommand Prepare(SqlConnection db,string sql,(string Name,object? Value)[] values)
    {
        var command = new SqlCommand(sql,db);
        foreach (var (name,value) in values)
        {
            if (value is DateTime date) command.Parameters.Add(name,SqlDbType.Date).Value = date;
            else command.Parameters.AddWithValue(name,value ?? DBNull.Value);
        }
        return command;
    }
}