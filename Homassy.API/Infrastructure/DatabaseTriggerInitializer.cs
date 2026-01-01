using Homassy.API.Context;
using Homassy.API.Entities.Common;
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
                    DECLARE
                        change_payload JSON;
                    BEGIN
                        INSERT INTO ""TableRecordChanges"" (""TableName"", ""RecordId"", ""IsDeleted"")
                        VALUES (TG_TABLE_NAME, NEW.""Id"", false);
                        
                        -- Send real-time notification
                        change_payload := json_build_object(
                            'table', TG_TABLE_NAME,
                            'recordId', NEW.""Id""
                        );
                        PERFORM pg_notify('cache_changes', change_payload::text);
                        
                        RETURN NEW;
                    EXCEPTION
                        WHEN OTHERS THEN
                            RAISE WARNING 'Error in record_table_change: %', SQLERRM;
                            RETURN NEW;
                    END;
                    $$ LANGUAGE plpgsql;
                ");

                var entityTypes = _context.Model.GetEntityTypes()
                    .Where(t => typeof(RecordChangeEntity).IsAssignableFrom(t.ClrType) && !t.ClrType.IsAbstract)
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
                        // Safe: triggerName and tableName are from EF Core metadata, not user input
#pragma warning disable EF1002
                        await _context.Database.ExecuteSqlRawAsync($@"
                            CREATE TRIGGER {triggerName}
                            AFTER INSERT OR UPDATE ON ""{tableName}""
                            FOR EACH ROW
                            EXECUTE FUNCTION record_table_change();
                        ");
#pragma warning restore EF1002

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
