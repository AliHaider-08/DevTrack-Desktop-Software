using System.Windows;
using Devzone.Infrastructure;
using Devzone.Infrastructure.Persistence;
using Devzone.Application.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var services = new ServiceCollection().AddDevzoneLocalServices();
            _serviceProvider = services.BuildServiceProvider();
            await using var scope = _serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevzoneDbContext>();
            await SqliteDatabase.InitializeAsync(dbContext);
            var setupRequired = !await dbContext.Users.AnyAsync();
            var accessWindow = new AccessWindow(_serviceProvider, setupRequired);
            MainWindow = accessWindow;
            var accessGranted = accessWindow.ShowDialog() == true;
            if (!accessGranted || accessWindow.User is null)
            {
                Shutdown();
                return;
            }

            using var authenticatedScope = _serviceProvider.CreateScope();
            var auditTrail = authenticatedScope.ServiceProvider.GetRequiredService<Devzone.Application.Auditing.IAuditTrail>();
            await auditTrail.AppendAsync(new Devzone.Application.Auditing.AuditEvent(
                Guid.NewGuid(), accessWindow.User.Id, accessWindow.User.Username, DateTimeOffset.UtcNow,
                "LoginSucceeded", "Security", accessWindow.User.Id.ToString("D"), null, null, Guid.NewGuid().ToString("N")));
            var company = await dbContext.Companies.AsNoTracking().SingleAsync(item => item.Id == accessWindow.User.CompanyId);
            var mainWindow = new MainWindow(accessWindow.User, company.BusinessName, company.CurrencyCode, _serviceProvider);
            MainWindow = mainWindow;
            mainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception exception)
        {
            MessageBox.Show($"DevTrack could not initialize its local database.\n\n{exception.Message}",
                "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
