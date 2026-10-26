using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Devzone.Application.Auditing;
using Devzone.Application.Security;
using Devzone.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Devzone.Desktop;

public sealed class AccessWindow : Window
{
    private const int MaximumLoginAttempts = 5;
    private readonly ServiceProvider _services;
    private readonly bool _setupRequiredAtLaunch;
    private bool _setupMode;
    private StackPanel _form = null!;
    private TextBlock _message = null!;
    private int _failedAttempts;
    private bool _lockedOut;
    private DispatcherTimer? _copyTypingTimer;
    private TextBlock? _heroTitle;
    private TextBlock? _heroDescription;
    private string _fullHeroTitle = string.Empty;
    private string _fullHeroDescription = string.Empty;
    private int _typingPosition;

    private TextBox _businessName = null!;
    private ComboBox _businessType = null!;
    private TextBox _currency = null!;
    private TextBox _ownerName = null!;
    private TextBox _username = null!;
    private PasswordBox _password = null!;
    private PasswordBox _confirmation = null!;

    public AuthenticatedUser? User { get; private set; }

    public AccessWindow(ServiceProvider services, bool setupRequired)
    {
        _services = services;
        _setupRequiredAtLaunch = setupRequired;
        _setupMode = setupRequired;
        Width = 940;
        MinWidth = 880;
        Opacity = 0;
        Height = 700;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)System.Windows.Application.Current.FindResource("PrimaryBackgroundBrush");
        Foreground = (Brush)System.Windows.Application.Current.FindResource("PrimaryTextBrush");
        FontFamily = new FontFamily("Segoe UI");
        Content = BuildContent();
        Loaded += (_, _) => AnimateIn();
    }

    private UIElement BuildContent()
    {
        _form = new StackPanel();
        _message = new TextBlock();
        var businessName = _businessName?.Text ?? string.Empty;
        var currency = _currency?.Text ?? "USD";
        var ownerName = _ownerName?.Text ?? string.Empty;
        var username = _username?.Text ?? string.Empty;
        var password = _password?.Password ?? string.Empty;
        var confirmation = _confirmation?.Password ?? string.Empty;
        var shell = new Border
        {
            Background = BrushFrom("SurfaceBrush"), CornerRadius = new CornerRadius(28), Margin = new Thickness(28),
            ClipToBounds = true,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { Color = Color.FromRgb(9, 25, 36), Opacity = 0.18, BlurRadius = 42, ShadowDepth = 16 }
        };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });

        var hero = CreateHeroPanel();
        Grid.SetColumn(hero, 0);
        layout.Children.Add(hero);
        var formSurface = new Border { Background = BrushFrom("SurfaceBrush"), Padding = new Thickness(54, 42, 54, 38) };
        Grid.SetColumn(formSurface, 1);
        layout.Children.Add(formSurface);
        var formLayout = new Grid();
        formLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        formLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        formSurface.Child = formLayout;

        var brandRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brandRow.Children.Add(new TextBlock { Text = "DEVTRACK", FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = (Brush)System.Windows.Application.Current.FindResource("PrimaryTextBrush") });
        var formHeader = new StackPanel();
        formHeader.Children.Add(brandRow);
        formHeader.Children.Add(new TextBlock
        {
            Text = _setupMode ? "Create your workspace" : "Welcome back",
            FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 42, 0, 7)
        });
        formHeader.Children.Add(new TextBlock { Text = _setupMode ? "Business details" : "Sign in with your account",
            FontSize = 13, Foreground = BrushFrom("SecondaryTextBrush"), Margin = new Thickness(0, 0, 0, 12) });
        Grid.SetRow(formHeader, 0);
        formLayout.Children.Add(formHeader);
        var formHost = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
        Grid.SetRow(formHost, 1);
        formLayout.Children.Add(formHost);
        var formScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = _setupMode ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = PanningMode.VerticalOnly,
            VerticalAlignment = VerticalAlignment.Stretch,
            Focusable = false
        };
        formHost.Children.Add(formScroll);
        if (_setupMode)
        {
            _businessName = AddTextField("Business name");
            _businessName.Text = businessName;
            _businessType = AddBusinessTypeSelector();
            _ownerName = AddTextField("Owner full name");
            _ownerName.Text = ownerName;
            _currency = AddTextField("Currency code (e.g. USD)");
            _currency.Text = currency;
        }
        _username = AddTextField("Username");
        _username.Text = username;
        _password = AddPasswordField("Password (at least 12 characters)");
        _password.Password = password;
        if (_setupMode)
        {
            _confirmation = AddPasswordField("Confirm password");
            _confirmation.Password = confirmation;
        }
        else
        {
            _businessName = null!;
            _businessType = null!;
            _currency = null!;
            _ownerName = null!;
            _confirmation = null!;
        }
        _message.Foreground = BrushFrom("DangerBrush");
        _message.TextWrapping = TextWrapping.Wrap;
        _message.Margin = new Thickness(0, 10, 0, 0);
        _form.Children.Add(_message);
        var action = new Button
        {
            Content = _setupMode ? "Create business and owner" : "Sign in",
            Height = 48,
            Margin = new Thickness(0, 20, 0, 0),
            Background = BrushFrom("AccentBrush"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            IsDefault = true
        };
        action.Template = CreateActionButtonTemplate();
        action.Click += Submit;
        _form.Children.Add(action);
        if (!_setupMode)
        {
            var createBusiness = new Button
            {
                Content = "Create a new business",
                Height = 34,
                Margin = new Thickness(0, 8, 0, 0),
                Background = Brushes.Transparent,
                Foreground = BrushFrom("AccentBrush"),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            createBusiness.Click += (_, _) =>
            {
                _setupMode = true;
                RebuildContent();
            };
            _form.Children.Add(createBusiness);
        }
        else if (!_setupRequiredAtLaunch)
        {
            var backToSignIn = new Button
            {
                Content = "Back to sign in",
                Height = 34,
                Margin = new Thickness(0, 8, 0, 0),
                Background = Brushes.Transparent,
                Foreground = BrushFrom("AccentBrush"),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            backToSignIn.Click += (_, _) =>
            {
                _setupMode = false;
                RebuildContent();
            };
            _form.Children.Add(backToSignIn);
        }
        formScroll.Content = _form;
        shell.Child = layout;
        return shell;
    }

    private void RebuildContent()
    {
        Content = BuildContent();
        if (IsLoaded)
            AnimateIn();
    }

    private TextBox AddTextField(string label)
    {
        _form.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 9, 0, 6), FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("SecondaryTextBrush") });
        var input = new TextBox { Height = 48, Padding = new Thickness(15, 11, 15, 11), MaxLength = 200,
            Background = Brushes.White, BorderBrush = BrushFrom("BorderBrush"), BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center, FontSize = 14, CaretBrush = BrushFrom("AccentBrush") };
        input.Template = CreateInputTemplate();
        _form.Children.Add(input);
        return input;
    }

    private PasswordBox AddPasswordField(string label)
    {
        _form.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 9, 0, 6), FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("SecondaryTextBrush") });
        var input = new PasswordBox { Height = 48, Padding = new Thickness(15, 11, 15, 11), MaxLength = 256,
            Background = Brushes.White, BorderBrush = BrushFrom("BorderBrush"), BorderThickness = new Thickness(1), FontSize = 14, CaretBrush = BrushFrom("AccentBrush") };
        input.Template = CreatePasswordTemplate();
        _form.Children.Add(input);
        return input;
    }

    private ComboBox AddBusinessTypeSelector()
    {
        _form.Children.Add(new TextBlock { Text = "Business type", Margin = new Thickness(0, 9, 0, 6), FontSize = 12,
            FontWeight = FontWeights.SemiBold, Foreground = BrushFrom("SecondaryTextBrush") });
        var selector = new ComboBox
        {
            Height = 48, Padding = new Thickness(12, 8, 12, 8), FontSize = 14,
            Background = Brushes.White, BorderBrush = BrushFrom("BorderBrush"), BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            ItemsSource = new[]
            {
                "Retail shop", "Grocery or convenience store", "Clothing and fashion", "Electronics",
                "Restaurant or café", "Wholesale and distribution", "Pharmacy", "Beauty or salon",
                "Home services", "Other"
            }
        };
        selector.SelectedItem = _businessType?.SelectedItem ?? "Retail shop";
        _form.Children.Add(selector);
        return selector;
    }

    private static Brush BrushFrom(string key) => (Brush)System.Windows.Application.Current.FindResource(key);

    private UIElement CreateHeroPanel()
    {
        var panel = new Grid { ClipToBounds = true };
        panel.Background = BrushFrom("HeroBrush");
        var content = new Grid { Margin = new Thickness(48, 48, 42, 44) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var brand = new TextBlock { Text = "DEVTRACK", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
        Grid.SetRow(brand, 0);
        content.Children.Add(brand);
        var heroCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 360 };
        _fullHeroTitle = _setupMode ? "Start with a clear view of your business." : "Your business, right where you left it.";
        _heroTitle = new TextBlock
        {
            FontSize = 31, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap, MinHeight = 78
        };
        heroCopy.Children.Add(_heroTitle);
        heroCopy.Children.Add(new Border { Width = 46, Height = 3, Background = new SolidColorBrush(Color.FromRgb(111, 190, 177)),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 0) });
        _fullHeroDescription = _setupMode
            ? "Create your workspace to bring products, customers and everyday operations together in one place."
            : "Sign in to review stock, keep customer records up to date and continue managing daily activity.";
        _heroDescription = new TextBlock
        {
            FontSize = 15, LineHeight = 24, Foreground = new SolidColorBrush(Color.FromArgb(215, 255, 255, 255)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 22, 12, 0), MinHeight = 72
        };
        heroCopy.Children.Add(_heroDescription);
        Grid.SetRow(heroCopy, 1);
        content.Children.Add(heroCopy);
        var footer = new TextBlock { Text = "INVENTORY  ·  CUSTOMERS  ·  ACCOUNTS", FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)) };
        Grid.SetRow(footer, 2);
        content.Children.Add(footer);
        panel.Children.Add(content);
        StartHeroCopyTyping();
        return panel;
    }

    private void StartHeroCopyTyping()
    {
        _copyTypingTimer?.Stop();
        if (_heroTitle is null || _heroDescription is null)
            return;

        _heroTitle.Text = string.Empty;
        _heroDescription.Text = string.Empty;
        _typingPosition = 0;
        _copyTypingTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(24)
        };
        _copyTypingTimer.Tick += (_, _) =>
        {
            var totalLength = _fullHeroTitle.Length + _fullHeroDescription.Length;
            if (_typingPosition < _fullHeroTitle.Length)
                _heroTitle.Text = _fullHeroTitle[..++_typingPosition];
            else if (_typingPosition < totalLength)
            {
                var descriptionPosition = _typingPosition++ - _fullHeroTitle.Length;
                _heroDescription.Text = _fullHeroDescription[..(descriptionPosition + 1)];
            }
            else
                _copyTypingTimer.Stop();
        };
        _copyTypingTimer.Start();
    }

    private static ControlTemplate CreateInputTemplate() => CreateFieldTemplate(typeof(TextBox));
    private static ControlTemplate CreatePasswordTemplate() => CreateFieldTemplate(typeof(PasswordBox));

    private static ControlTemplate CreateFieldTemplate(Type targetType)
    {
        var template = new ControlTemplate(targetType);
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "FieldBorder";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        var host = new FrameworkElementFactory(typeof(ScrollViewer));
        host.Name = "PART_ContentHost";
        border.AppendChild(host);
        template.VisualTree = border;
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, BrushFrom("AccentBrush"), "FieldBorder"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1.6), "FieldBorder"));
        template.Triggers.Add(focus);
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, BrushFrom("AccentHoverBrush"), "FieldBorder"));
        template.Triggers.Add(hover);
        return template;
    }

    private static ControlTemplate CreateActionButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ActionBorder";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        border.AppendChild(content);
        template.VisualTree = border;
        var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "ActionBorder"));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.76, "ActionBorder"));
        template.Triggers.Add(pressed);
        return template;
    }

    private void AnimateIn()
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(OpacityProperty, fade);
        if (Content is FrameworkElement root)
        {
            root.RenderTransform = new TranslateTransform(0, 12);
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            ((TranslateTransform)root.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(420))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    private async void Submit(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            button.IsEnabled = false;
        _message.Text = string.Empty;
        try
        {
            await using var scope = _services.CreateAsyncScope();
            if (_setupMode)
            {
                if (_password.Password != _confirmation.Password)
                    throw new ArgumentException("The passwords do not match.");
                var setup = scope.ServiceProvider.GetRequiredService<IOwnerSetupService>();
                await setup.CreateOwnerAsync(new OwnerSetupRequest(
                    _businessName.Text, _businessType.SelectedItem?.ToString() ?? "Other", _currency.Text, _ownerName.Text, _username.Text,
                    _password.Password, new DateOnly(DateTime.Today.Year, 1, 1)));
            }

            var authenticator = scope.ServiceProvider.GetRequiredService<IUserAuthenticator>();
            User = await authenticator.AuthenticateAsync(_username.Text, _password.Password);
            if (User is null)
            {
                if (!_setupMode)
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
