using Homassy.API.Context;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Infrastructure
{
    public class DatabaseTriggerInitializer
    {
        private readonly HomassyDbContext _context;

        public DatabaseTriggerInitializer(HomassyDbContext context)
        {
            _context = context;
        }

        public async Task InitializeTriggersAsync()
        {
            try
            {
                Log.Information("Checking and creating database triggers...");

                await _context.Database.ExecuteSqlRawAsync(@"
                    CREATE OR REPLACE FUNCTION record_table_change()
                    RETURNS TRIGGER AS $$
                    BEGIN
                        INSERT INTO ""TableRecordChanges"" (""TableName"", ""RecordId"", ""IsDeleted"", ""RecordChange"")
                        VALUES (TG_TABLE_NAME, NEW.""Id"", false, NOW()::text);
                        RETURN NEW;
                    EXCEPTION
                        WHEN OTHERS THEN
                            RAISE WARNING 'Error inserting into TableRecordChanges: %', SQLERRM;
                            RETURN NEW;
                    END;
                    $$ LANGUAGE plpgsql;
                ");

                var entityTypes = _context.Model.GetEntityTypes()
                    .Where(t => t.ClrType.BaseType?.Name == "BaseEntity")
                    .Select(t => t.GetTableName())
                    .Where(name => !string.IsNullOrEmpty(name)
                                && name != "TableRecordChanges"
                                && name != "__EFMigrationsHistory");

                foreach (var tableName in entityTypes)
                {
                    var triggerName = $"tr_{tableName!.ToLower()}_record_changes";

                    var connection = _context.Database.GetDbConnection();

                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT COUNT(*)
                        FROM information_schema.triggers
                        WHERE trigger_name = @triggerName";

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@triggerName";
                    parameter.Value = triggerName;
                    command.Parameters.Add(parameter);

                    var result = await command.ExecuteScalarAsync();
                    var triggerExists = Convert.ToInt32(result) > 0;

                    if (!triggerExists)
                    {
                        await _context.Database.ExecuteSqlRawAsync($@"
                            CREATE TRIGGER {triggerName}
                            AFTER INSERT OR UPDATE ON ""{tableName}""
                            FOR EACH ROW
                            EXECUTE FUNCTION record_table_change();
                        ");

                        Log.Information($"Created trigger for table: {tableName}");
                    }
                    else
                    {
                        Log.Debug($"Trigger already exists for table: {tableName}");
                    }
                }

                Log.Information("Database trigger initialization completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing database triggers");
            }
        }
    }
}
