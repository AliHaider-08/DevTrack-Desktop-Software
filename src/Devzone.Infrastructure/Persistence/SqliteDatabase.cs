using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Devzone.Infrastructure.Persistence;

public static class SqliteDatabase
{
    public static string GetDefaultDatabasePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("DEVZONE_DATABASE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!Path.IsPathFullyQualified(configuredPath))
                throw new InvalidOperationException("DEVZONE_DATABASE_PATH must be an absolute file path.");
            return configuredPath;
        }

        var localSettingsPath = Path.Combine(AppContext.BaseDirectory, "devzone.local.json");
        if (File.Exists(localSettingsPath))
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(localSettingsPath));
            if (settings.RootElement.TryGetProperty("databasePath", out var databasePathElement))
            {
                var localDatabasePath = databasePathElement.GetString();
                if (string.IsNullOrWhiteSpace(localDatabasePath) || !Path.IsPathFullyQualified(localDatabasePath))
                    throw new InvalidOperationException("The local database path must be an absolute file path.");
                return localDatabasePath;
            }
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            throw new InvalidOperationException("Windows did not provide a LocalAppData folder.");
        return Path.Combine(appData, "Devzone System", "BusinessManagement", "devzone.db");
    }

    public static DbContextOptions<DevzoneDbContext> CreateOptions(string? databasePath = null)
    {
        var connectionString = CreateConnectionString(databasePath);
        return new DbContextOptionsBuilder<DevzoneDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public static string CreateConnectionString(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The database path must be absolute.", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("The database path must include a directory.", nameof(databasePath));
        }

        Directory.CreateDirectory(directory);
        return $"Data Source={path};Foreign Keys=True;Pooling=True";
    }

    public static async Task InitializeAsync(DevzoneDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureBusinessTypeColumnAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS Categories (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, Name TEXT NOT NULL, NormalizedName TEXT NOT NULL, Description TEXT NULL, CreatedAt TEXT NOT NULL, FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE RESTRICT);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_CompanyId_NormalizedName ON Categories (CompanyId, NormalizedName);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS Products (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, Sku TEXT NOT NULL, NormalizedSku TEXT NOT NULL, Barcode TEXT NULL, Name TEXT NOT NULL, Description TEXT NULL, CategoryId TEXT NULL, CostPrice TEXT NOT NULL, SellingPrice TEXT NOT NULL, MinimumStock TEXT NOT NULL, IsActive INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE RESTRICT, FOREIGN KEY (CategoryId) REFERENCES Categories (Id) ON DELETE RESTRICT);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Products_CompanyId_NormalizedSku ON Products (CompanyId, NormalizedSku);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Products_CompanyId_Barcode ON Products (CompanyId, Barcode);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS StockMovements (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, ProductId TEXT NOT NULL, QuantityChange TEXT NOT NULL, BalanceAfter TEXT NOT NULL, UnitCost TEXT NOT NULL, Reason TEXT NOT NULL, Username TEXT NOT NULL, OccurredAt TEXT NOT NULL, FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE RESTRICT, FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_StockMovements_CompanyId_OccurredAt ON StockMovements (CompanyId, OccurredAt);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS Customers (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, Code TEXT NOT NULL, NormalizedCode TEXT NOT NULL, Name TEXT NOT NULL, NormalizedName TEXT NOT NULL, Phone TEXT NULL, Email TEXT NULL, Address TEXT NULL, CreditLimit TEXT NOT NULL, IsActive INTEGER NOT NULL, CreatedAt TEXT NOT NULL, FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE RESTRICT);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Customers_CompanyId_NormalizedCode ON Customers (CompanyId, NormalizedCode);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Customers_CompanyId_NormalizedName ON Customers (CompanyId, NormalizedName);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS CustomerLedgerEntries (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, CustomerId TEXT NOT NULL, OccurredAt TEXT NOT NULL, Type TEXT NOT NULL, BalanceChange TEXT NOT NULL, BalanceAfter TEXT NOT NULL, Description TEXT NOT NULL, Username TEXT NOT NULL, FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE RESTRICT, FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE RESTRICT);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_CustomerLedgerEntries_CompanyId_CustomerId_OccurredAt ON CustomerLedgerEntries (CompanyId, CustomerId, OccurredAt);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS Purchases (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, ProductId TEXT NOT NULL, ReceivedAt TEXT NOT NULL, Reference TEXT NOT NULL, SupplierName TEXT NOT NULL, Quantity TEXT NOT NULL, UnitCost TEXT NOT NULL, TotalCost TEXT NOT NULL, Username TEXT NOT NULL, FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE RESTRICT, FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Purchases_CompanyId_ReceivedAt ON Purchases (CompanyId, ReceivedAt);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout = 5000;", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS TR_AuditEvents_NoUpdate BEFORE UPDATE ON AuditEvents BEGIN SELECT RAISE(ABORT, 'Audit events are append-only'); END;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS TR_AuditEvents_NoDelete BEFORE DELETE ON AuditEvents BEGIN SELECT RAISE(ABORT, 'Audit events are append-only'); END;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS TR_CustomerLedger_NoUpdate BEFORE UPDATE ON CustomerLedgerEntries BEGIN SELECT RAISE(ABORT, 'Customer ledger entries are append-only'); END;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS TR_CustomerLedger_NoDelete BEFORE DELETE ON CustomerLedgerEntries BEGIN SELECT RAISE(ABORT, 'Customer ledger entries are append-only'); END;",
            cancellationToken);
    }

    private static async Task EnsureBusinessTypeColumnAsync(DevzoneDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Companies');";
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hasBusinessType = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), "BusinessType", StringComparison.OrdinalIgnoreCase))
            {
                hasBusinessType = true;
                break;
            }
        }
        await reader.DisposeAsync();
        if (!hasBusinessType)
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Companies ADD COLUMN BusinessType TEXT NOT NULL DEFAULT 'Other';", cancellationToken);
    }
}
