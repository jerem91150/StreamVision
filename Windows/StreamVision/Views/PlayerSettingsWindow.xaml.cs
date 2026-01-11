using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using StreamVision.Models;
using StreamVision.Services;

namespace StreamVision.Views
{
    public partial class PlayerSettingsWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private PlayerSettings _settings;
        private bool _isLoading = true;

        public PlayerSettings Settings => _settings;
        public bool SettingsChanged { get; private set; }

        public PlayerSettingsWindow(PlayerSettings? existingSettings = null)
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _settings = existingSettings ?? new PlayerSettings();

            LoadSettingsToUI();
            _isLoading = false;
        }

        private void LoadSettingsToUI()
        {
            // Buffer settings
            NetworkCachingSlider.Value = _settings.NetworkCaching;
            LiveCachingSlider.Value = _settings.LiveCaching;
            FileCachingSlider.Value = _settings.FileCaching;
            MinBufferSlider.Value = _settings.MinBufferBeforePlay;

            // Decoding settings
            HardwareAccelCheckbox.IsChecked = _settings.HardwareAcceleration;
            SetComboBoxByTag(HWTypeCombo, _settings.HardwareAccelerationType);
            SkipFramesCheckbox.IsChecked = _settings.SkipFramesOnLag;
            FastSeekCheckbox.IsChecked = _settings.FastSeek;
            ThreadsSlider.Value = _settings.DecodingThreads;

            // Network settings
            AutoReconnectCheckbox.IsChecked = _settings.AutoReconnect;
            ReconnectAttemptsSlider.Value = _settings.ReconnectAttempts;
            ConnectionTimeoutSlider.Value = _settings.ConnectionTimeout;
            PreferIPv4Checkbox.IsChecked = _settings.PreferIPv4;
            UserAgentTextBox.Text = _settings.UserAgent;

            // Audio settings
            AudioDelaySlider.Value = _settings.AudioDelay;
            VolumeBoostSlider.Value = _settings.VolumeBoost;
            AudioNormalizeCheckbox.IsChecked = _settings.AudioNormalize;
            AudioTimeStretchCheckbox.IsChecked = _settings.AudioTimeStretch;
            SetComboBoxByTag(AudioOutputCombo, _settings.AudioOutput);

            // Video settings
            SubtitleDelaySlider.Value = _settings.SubtitleDelay;
            SetComboBoxByTag(DeinterlaceCombo, _settings.Deinterlace);
            SetComboBoxByTag(DeinterlaceModeCombo, _settings.DeinterlaceMode);
            PostProcessingCheckbox.IsChecked = _settings.PostProcessing;
            SetComboBoxByTag(VideoOutputCombo, _settings.VideoOutput);

            // Playback settings
            RememberPositionCheckbox.IsChecked = _settings.RememberPosition;
            AutoPlayNextCheckbox.IsChecked = _settings.AutoPlayNext;
            SkipIntroSlider.Value = _settings.SkipIntroSeconds;
            AdaptiveQualityCheckbox.IsChecked = _settings.AdaptiveQuality;

            UpdatePresetDescription();
        }

        private void SetComboBoxByTag(ComboBox comboBox, string tag)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private string GetComboBoxTag(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? "";
            }
            return "";
        }

        private void SaveUIToSettings()
        {
            // Buffer settings
            _settings.NetworkCaching = (int)NetworkCachingSlider.Value;
            _settings.LiveCaching = (int)LiveCachingSlider.Value;
            _settings.FileCaching = (int)FileCachingSlider.Value;
            _settings.MinBufferBeforePlay = (int)MinBufferSlider.Value;

            // Decoding settings
            _settings.HardwareAcceleration = HardwareAccelCheckbox.IsChecked ?? true;
            _settings.HardwareAccelerationType = GetComboBoxTag(HWTypeCombo);
            _settings.SkipFramesOnLag = SkipFramesCheckbox.IsChecked ?? true;
            _settings.FastSeek = FastSeekCheckbox.IsChecked ?? true;
            _settings.DecodingThreads = (int)ThreadsSlider.Value;

            // Network settings
            _settings.AutoReconnect = AutoReconnectCheckbox.IsChecked ?? true;
            _settings.ReconnectAttempts = (int)ReconnectAttemptsSlider.Value;
            _settings.ConnectionTimeout = (int)ConnectionTimeoutSlider.Value;
            _settings.PreferIPv4 = PreferIPv4Checkbox.IsChecked ?? true;
            _settings.UserAgent = UserAgentTextBox.Text;

            // Audio settings
            _settings.AudioDelay = (int)AudioDelaySlider.Value;
            _settings.VolumeBoost = (int)VolumeBoostSlider.Value;
            _settings.AudioNormalize = AudioNormalizeCheckbox.IsChecked ?? false;
            _settings.AudioTimeStretch = AudioTimeStretchCheckbox.IsChecked ?? true;
            _settings.AudioOutput = GetComboBoxTag(AudioOutputCombo);

            // Video settings
            _settings.SubtitleDelay = (int)SubtitleDelaySlider.Value;
            _settings.Deinterlace = GetComboBoxTag(DeinterlaceCombo);
            _settings.DeinterlaceMode = GetComboBoxTag(DeinterlaceModeCombo);
            _settings.PostProcessing = PostProcessingCheckbox.IsChecked ?? false;
            _settings.VideoOutput = GetComboBoxTag(VideoOutputCombo);

            // Playback settings
            _settings.RememberPosition = RememberPositionCheckbox.IsChecked ?? true;
            _settings.AutoPlayNext = AutoPlayNextCheckbox.IsChecked ?? true;
            _settings.SkipIntroSeconds = (int)SkipIntroSlider.Value;
            _settings.AdaptiveQuality = AdaptiveQualityCheckbox.IsChecked ?? true;

            _settings.UpdatedAt = DateTime.Now;
        }

        private void UpdatePresetDescription()
        {
            var hw = _settings.HardwareAcceleration ? "GPU" : "CPU";
            var reconnect = _settings.AutoReconnect ? "Oui" : "Non";
            CurrentPresetDesc.Text = $"Buffer: {_settings.NetworkCaching}ms | Reconnexion: {reconnect} | Décodage: {hw}";
        }

        #region Tab Navigation

        private void Tab_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            PresetsPanel.Visibility = TabPresets.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            BufferPanel.Visibility = TabBuffer.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            DecodingPanel.Visibility = TabDecoding.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            NetworkPanel.Visibility = TabNetwork.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            AudioPanel.Visibility = TabAudio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            VideoPanel.Visibility = TabVideo.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PlaybackPanel.Visibility = TabPlayback.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Update tip based on current tab
            if (TabBuffer.IsChecked == true)
                TipText.Text = "Augmentez le cache réseau si vous avez des micro-coupures fréquentes";
            else if (TabDecoding.IsChecked == true)
                TipText.Text = "Désactivez l'accélération matérielle si la vidéo affiche des artefacts";
            else if (TabNetwork.IsChecked == true)
                TipText.Text = "Activez la reconnexion auto pour les flux IPTV instables";
            else if (TabAudio.IsChecked == true)
                TipText.Text = "Ajustez le délai audio si le son n'est pas synchronisé avec l'image";
            else if (TabVideo.IsChecked == true)
                TipText.Text = "Activez le désentrelacement pour les sources TV analogiques";
            else if (TabPlayback.IsChecked == true)
                TipText.Text = "La qualité adaptative réduit automatiquement la résolution si votre connexion est lente";
            else
                TipText.Text = "Conseil : Essayez le préréglage 'Stable' si vous avez des coupures fréquentes";
        }

        #endregion

        #region Presets

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string presetName)
            {
                _settings = PlayerSettings.GetPreset(presetName);
                _isLoading = true;
                LoadSettingsToUI();
                _isLoading = false;

                CurrentPresetName.Text = presetName switch
                {
                    "stable" => "Stable",
                    "lowlatency" => "Faible latence",
                    "quality" => "Qualité maximale",
                    "slowconnection" => "Connexion lente",
                    "compatibility" => "Compatibilité",
                    _ => "Par défaut"
                };

                UpdatePresetDescription();
                SettingsChanged = true;

                MessageBox.Show($"Préréglage '{CurrentPresetName.Text}' appliqué !\n\nCliquez sur 'Appliquer' pour sauvegarder.",
                    "Préréglage", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Slider Value Changed Events

        private void NetworkCaching_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            NetworkCachingValue.Text = $"{(int)e.NewValue} ms";
            SettingsChanged = true;
        }

        private void LiveCaching_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            LiveCachingValue.Text = $"{(int)e.NewValue} ms";
            SettingsChanged = true;
        }

        private void FileCaching_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            FileCachingValue.Text = $"{(int)e.NewValue} ms";
            SettingsChanged = true;
        }

        private void MinBuffer_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            MinBufferValue.Text = $"{(int)e.NewValue}%";
            SettingsChanged = true;
        }

        private void Threads_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            ThreadsValue.Text = (int)e.NewValue == 0 ? "Auto" : $"{(int)e.NewValue}";
            SettingsChanged = true;
        }

        private void ReconnectAttempts_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            ReconnectAttemptsValue.Text = $"{(int)e.NewValue}";
            SettingsChanged = true;
        }

        private void ConnectionTimeout_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            ConnectionTimeoutValue.Text = $"{(int)e.NewValue / 1000} sec";
            SettingsChanged = true;
        }

        private void AudioDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            AudioDelayValue.Text = $"{(int)e.NewValue} ms";
            SettingsChanged = true;
        }

        private void VolumeBoost_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            VolumeBoostValue.Text = $"{(int)e.NewValue}%";
            SettingsChanged = true;
        }

        private void SubtitleDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            SubtitleDelayValue.Text = $"{(int)e.NewValue} ms";
            SettingsChanged = true;
        }

        private void SkipIntro_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            SkipIntroValue.Text = (int)e.NewValue == 0 ? "Désactivé" : $"{(int)e.NewValue} sec";
            SettingsChanged = true;
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            SettingsChanged = true;
        }

        private void Setting_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            SettingsChanged = true;
        }

        private void Setting_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            SettingsChanged = true;
        }

        #endregion

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Réinitialiser tous les paramètres par défaut ?",
                "Réinitialiser", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _settings = new PlayerSettings();
                _isLoading = true;
                LoadSettingsToUI();
                _isLoading = false;
                CurrentPresetName.Text = "Par défaut";
                UpdatePresetDescription();
                SettingsChanged = true;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SaveUIToSettings();
            DialogResult = true;
            Close();
        }

        #endregion
    }

    /// <summary>
    /// Converter for slider track width (simplified version)
    /// </summary>
    public class SliderValueToWidthConverter : IValueConverter
    {
        public static SliderValueToWidthConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // This is a simplified converter - actual implementation would need parent width
            return 100.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
