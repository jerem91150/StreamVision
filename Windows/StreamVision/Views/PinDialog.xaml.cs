using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StreamVision.Views
{
    public partial class PinDialog : Window
    {
        private string _enteredPin = "";
        private readonly string? _correctPin;
        private readonly bool _isSettingNewPin;
        private string? _firstPin;

        public bool IsAuthenticated { get; private set; }
        public string? NewPin { get; private set; }

        /// <summary>
        /// Creates a PIN dialog for authentication
        /// </summary>
        /// <param name="correctPin">The correct PIN to validate against. If null, dialog is in "set new PIN" mode.</param>
        public PinDialog(string? correctPin = null)
        {
            InitializeComponent();
            _correctPin = correctPin;
            _isSettingNewPin = string.IsNullOrEmpty(correctPin);

            if (_isSettingNewPin)
            {
                TitleText.Text = "Créer un code PIN";
                SubtitleText.Text = "Entrez un code à 4 chiffres";
            }

            Loaded += (s, e) => PinInput.Focus();
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string digit)
            {
                if (_enteredPin.Length < 4)
                {
                    _enteredPin += digit;
                    UpdatePinDisplay();

                    if (_enteredPin.Length == 4)
                    {
                        ValidatePin();
                    }
                }
            }
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (_enteredPin.Length > 0)
            {
                _enteredPin = _enteredPin.Substring(0, _enteredPin.Length - 1);
                UpdatePinDisplay();
                ErrorText.Text = "";
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (_enteredPin.Length == 4)
            {
                ValidatePin();
            }
            else
            {
                ErrorText.Text = "Entrez 4 chiffres";
                ShakeWindow();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsAuthenticated = false;
            DialogResult = false;
            Close();
        }

        private void PinInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Sync hidden textbox with visual display
            string text = PinInput.Text;
            _enteredPin = "";
            foreach (char c in text)
            {
                if (char.IsDigit(c) && _enteredPin.Length < 4)
                {
                    _enteredPin += c;
                }
            }
            PinInput.Text = _enteredPin;
            PinInput.CaretIndex = _enteredPin.Length;
            UpdatePinDisplay();

            if (_enteredPin.Length == 4)
            {
                ValidatePin();
            }
        }

        private void PinInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _enteredPin.Length == 4)
            {
                ValidatePin();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
                e.Handled = true;
            }
        }

        private void UpdatePinDisplay()
        {
            Pin1.Text = _enteredPin.Length >= 1 ? "●" : "";
            Pin2.Text = _enteredPin.Length >= 2 ? "●" : "";
            Pin3.Text = _enteredPin.Length >= 3 ? "●" : "";
            Pin4.Text = _enteredPin.Length >= 4 ? "●" : "";
        }

        private void ValidatePin()
        {
            if (_isSettingNewPin)
            {
                if (_firstPin == null)
                {
                    // First entry - ask to confirm
                    _firstPin = _enteredPin;
                    _enteredPin = "";
                    UpdatePinDisplay();
                    TitleText.Text = "Confirmer le code PIN";
                    SubtitleText.Text = "Entrez à nouveau le code";
                    ErrorText.Text = "";
                }
                else
                {
                    // Second entry - validate match
                    if (_enteredPin == _firstPin)
                    {
                        NewPin = _enteredPin;
                        IsAuthenticated = true;
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        ErrorText.Text = "Les codes ne correspondent pas";
                        ShakeWindow();
                        _firstPin = null;
                        _enteredPin = "";
                        UpdatePinDisplay();
                        TitleText.Text = "Créer un code PIN";
                        SubtitleText.Text = "Entrez un code à 4 chiffres";
                    }
                }
            }
            else
            {
                // Validating existing PIN
                if (_enteredPin == _correctPin)
                {
                    IsAuthenticated = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ErrorText.Text = "Code PIN incorrect";
                    ShakeWindow();
                    _enteredPin = "";
                    UpdatePinDisplay();
                }
            }
        }

        private async void ShakeWindow()
        {
            double originalLeft = Left;
            for (int i = 0; i < 3; i++)
            {
                Left = originalLeft - 10;
                await System.Threading.Tasks.Task.Delay(50);
                Left = originalLeft + 10;
                await System.Threading.Tasks.Task.Delay(50);
            }
            Left = originalLeft;
        }
    }
}
