using System.Windows;
using System.Windows.Input;
using StreamVision.Services;

namespace StreamVision.Views
{
    public partial class SettingsWindow : Window
    {
        private string? _currentPin;
        private bool _isLoading = true;

        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += SettingsWindow_Loaded;
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

            using var db = new DatabaseService();
            await db.InitializeAsync();

            // Load parental control settings
            var parentalEnabled = await db.GetSettingAsync("ParentalControlEnabled");
            var pin = await db.GetSettingAsync("ParentalControlPin");
            var blockAdult = await db.GetSettingAsync("BlockAdultContent");
            var blockViolence = await db.GetSettingAsync("BlockViolenceContent");

            _currentPin = pin;
            ParentalControlToggle.IsChecked = parentalEnabled == "true";
            BlockAdultCheck.IsChecked = blockAdult == "true";
            BlockViolenceCheck.IsChecked = blockViolence == "true";

            UpdatePinUI();

            _isLoading = false;
        }

        private void UpdatePinUI()
        {
            bool isEnabled = ParentalControlToggle.IsChecked == true;
            PinSection.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            BlockedCategoriesSection.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (!string.IsNullOrEmpty(_currentPin))
            {
                PinStatusText.Text = "PIN configuré (****)";
                ChangePinBtn.Content = "Modifier";
            }
            else
            {
                PinStatusText.Text = "Non configuré";
                ChangePinBtn.Content = "Configurer";
            }
        }

        private void ParentalControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            if (ParentalControlToggle.IsChecked == true)
            {
                // Enabling - need to set up PIN if not configured
                if (string.IsNullOrEmpty(_currentPin))
                {
                    var pinDialog = new PinDialog(null); // Set new PIN mode
                    pinDialog.Owner = this;

                    if (pinDialog.ShowDialog() == true && !string.IsNullOrEmpty(pinDialog.NewPin))
                    {
                        _currentPin = pinDialog.NewPin;
                        BlockAdultCheck.IsChecked = true; // Default to blocking adult content
                    }
                    else
                    {
                        // User cancelled - disable parental control
                        ParentalControlToggle.IsChecked = false;
                    }
                }
            }
            else
            {
                // Disabling - require PIN to turn off
                if (!string.IsNullOrEmpty(_currentPin))
                {
                    var pinDialog = new PinDialog(_currentPin);
                    pinDialog.Owner = this;

                    if (pinDialog.ShowDialog() != true)
                    {
                        // Wrong PIN or cancelled - keep enabled
                        ParentalControlToggle.IsChecked = true;
                    }
                }
            }

            UpdatePinUI();
        }

        private void ChangePin_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentPin))
            {
                // Verify current PIN first
                var verifyDialog = new PinDialog(_currentPin);
                verifyDialog.Owner = this;

                if (verifyDialog.ShowDialog() != true)
                {
                    return; // Wrong PIN
                }
            }

            // Set new PIN
            var newPinDialog = new PinDialog(null);
            newPinDialog.Owner = this;

            if (newPinDialog.ShowDialog() == true && !string.IsNullOrEmpty(newPinDialog.NewPin))
            {
                _currentPin = newPinDialog.NewPin;
                UpdatePinUI();
                MessageBox.Show("Code PIN mis à jour avec succès!", "Succès",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            using var db = new DatabaseService();
            await db.InitializeAsync();

            // Save parental control settings
            await db.SaveSettingAsync("ParentalControlEnabled",
                ParentalControlToggle.IsChecked == true ? "true" : "false");

            if (!string.IsNullOrEmpty(_currentPin))
            {
                await db.SaveSettingAsync("ParentalControlPin", _currentPin);
            }

            await db.SaveSettingAsync("BlockAdultContent",
                BlockAdultCheck.IsChecked == true ? "true" : "false");
            await db.SaveSettingAsync("BlockViolenceContent",
                BlockViolenceCheck.IsChecked == true ? "true" : "false");

            DialogResult = true;
            Close();
        }
    }
}
