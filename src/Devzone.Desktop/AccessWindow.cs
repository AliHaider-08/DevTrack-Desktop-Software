using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Devzone.Application.Auditing;
using Devzone.Application.Security;
using Devzone.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Devzone.Desktop;

public sealed class AccessWindow : Window
{
    private const int MaximumLoginAttempts = 5;
    private readonly ServiceProvider _services;
    private readonly bool _setupRequired;
    private readonly StackPanel _form = new();
    private readonly TextBlock _message = new();
    private int _failedAttempts;
    private bool _lockedOut;

    private TextBox _businessName = null!;
    private TextBox _currency = null!;
    private TextBox _ownerName = null!;
    private TextBox _username = null!;
    private PasswordBox _password = null!;
    private PasswordBox _confirmation = null!;

    public AuthenticatedUser? User { get; private set; }

    public AccessWindow(ServiceProvider services, bool setupRequired)
    {
        _services = services;
        _setupRequired = setupRequired;
        Title = setupRequired ? "Set up DevTrack" : "Sign in to DevTrack";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)System.Windows.Application.Current.FindResource("PrimaryBackgroundBrush");
        Foreground = (Brush)System.Windows.Application.Current.FindResource("PrimaryTextBrush");
        FontFamily = new FontFamily("Segoe UI");
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var card = new Border
        {
            Background = (Brush)System.Windows.Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)System.Windows.Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(32),
            Margin = new Thickness(30)
        };
        var layout = new StackPanel();
        layout.Children.Add(new TextBlock { Text = "DEVTRACK", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = (Brush)System.Windows.Application.Current.FindResource("PrimaryTextBrush") });
        layout.Children.Add(new TextBlock
        {
            Text = _setupRequired ? "Create your company and owner account" : "Sign in to your workspace",
            FontSize = 17,
            Margin = new Thickness(0, 6, 0, 22)
        });
        _form.Children.Clear();
        if (_setupRequired)
        {
            _businessName = AddTextField("Business name");
            _ownerName = AddTextField("Owner full name");
            _currency = AddTextField("Currency code (e.g. USD)");
            _currency.Text = "USD";
        }
        _username = AddTextField("Username");
        _password = AddPasswordField("Password (at least 12 characters)");
        if (_setupRequired)
            _confirmation = AddPasswordField("Confirm password");
        _message.Foreground = Brushes.Firebrick;
        _message.TextWrapping = TextWrapping.Wrap;
        _message.Margin = new Thickness(0, 8, 0, 0);
        _form.Children.Add(_message);
        var action = new Button
        {
            Content = _setupRequired ? "Create company and owner" : "Sign in",
            Height = 42,
            Margin = new Thickness(0, 18, 0, 0),
            Background = (Brush)System.Windows.Application.Current.FindResource("SecondaryBackgroundBrush"),
            Foreground = (Brush)System.Windows.Application.Current.FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)System.Windows.Application.Current.FindResource("BorderBrush"),
            FontWeight = FontWeights.SemiBold,
            IsDefault = true
        };
        action.Click += Submit;
        _form.Children.Add(action);
        layout.Children.Add(_form);
        card.Child = layout;
        return card;
    }

    private TextBox AddTextField(string label)
    {
        _form.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4), FontSize = 12 });
        var input = new TextBox { Height = 34, Padding = new Thickness(9, 6, 9, 6), MaxLength = 200 };
        _form.Children.Add(input);
        return input;
    }

    private PasswordBox AddPasswordField(string label)
    {
        _form.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4), FontSize = 12 });
        var input = new PasswordBox { Height = 34, Padding = new Thickness(9, 6, 9, 6), MaxLength = 256 };
        _form.Children.Add(input);
        return input;
    }

    private async void Submit(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            button.IsEnabled = false;
        _message.Text = string.Empty;
        try
        {
            await using var scope = _services.CreateAsyncScope();
            if (_setupRequired)
            {
                if (_password.Password != _confirmation.Password)
                    throw new ArgumentException("The passwords do not match.");
                var setup = scope.ServiceProvider.GetRequiredService<IOwnerSetupService>();
                await setup.CreateFirstOwnerAsync(new OwnerSetupRequest(
                    _businessName.Text, _currency.Text, _ownerName.Text, _username.Text,
                    _password.Password, new DateOnly(DateTime.Today.Year, 1, 1)));
            }

            var authenticator = scope.ServiceProvider.GetRequiredService<IUserAuthenticator>();
            User = await authenticator.AuthenticateAsync(_username.Text, _password.Password);
            if (User is null)
            {
                if (!_setupRequired)
                {
                    await scope.ServiceProvider.GetRequiredService<IAuditTrail>().AppendAsync(new AuditEvent(
                        Guid.NewGuid(), null, NormalizeAuditUsername(_username.Text),
                        DateTimeOffset.UtcNow, "LoginFailed", "Security", "unknown", null, null, Guid.NewGuid().ToString("N")));
                    _failedAttempts++;
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(_failedAttempts, MaximumLoginAttempts) * 350));
                    if (_failedAttempts >= MaximumLoginAttempts)
                    {
                        _lockedOut = true;
                        _message.Text = "Too many failed attempts. Close and reopen the application to try again.";
                        return;
                    }
                }
                throw new UnauthorizedAccessException("The username or password is incorrect.");
            }

            DialogResult = true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException or DbUpdateException)
        {
            _message.Text = exception switch
            {
                DbUpdateException => "Could not save the setup details. Check that the username is available and try again.",
                UnauthorizedAccessException => exception.Message,
                _ => exception.Message
            };
        }
        finally
        {
            if (sender is Button submitButton)
                submitButton.IsEnabled = !_lockedOut;
        }
    }

    private static string NormalizeAuditUsername(string username)
    {
        var value = string.IsNullOrWhiteSpace(username) ? "unknown" : username.Trim();
        return value.Length <= 80 ? value : value[..80];
    }
}
