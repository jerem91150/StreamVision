using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using StreamVision.Models;

namespace StreamVision.Views
{
    /// <summary>
    /// Quality selector popup (YouTube-style)
    /// </summary>
    public partial class QualitySelectorPopup : UserControl
    {
        private StreamQuality _currentQuality = StreamQuality.Auto;
        private List<QualityDisplayItem> _qualityOptions = new();

        // Events
        public event Action<StreamQuality>? OnQualitySelected;

        public StreamQuality CurrentQuality
        {
            get => _currentQuality;
            set
            {
                _currentQuality = value;
                UpdateSelection();
            }
        }

        public QualitySelectorPopup()
        {
            InitializeComponent();
            InitializeDefaultOptions();
        }

        /// <summary>
        /// Set available quality options
        /// </summary>
        public void SetQualityOptions(List<QualityOption> options)
        {
            _qualityOptions.Clear();

            foreach (var opt in options)
            {
                _qualityOptions.Add(new QualityDisplayItem
                {
                    Quality = opt.Quality,
                    Label = opt.DisplayLabel,
                    Bitrate = opt.Bitrate,
                    IsSelected = opt.Quality == _currentQuality
                });
            }

            QualityItems.ItemsSource = _qualityOptions;
            UpdateSelection();
        }

        /// <summary>
        /// Initialize with default quality options
        /// </summary>
        private void InitializeDefaultOptions()
        {
            _qualityOptions = new List<QualityDisplayItem>
            {
                new() { Quality = StreamQuality.Ultra, Label = "4K", Bitrate = 25000, IsSelected = false },
                new() { Quality = StreamQuality.High, Label = "1080p", Bitrate = 8000, IsSelected = false },
                new() { Quality = StreamQuality.Medium, Label = "720p", Bitrate = 5000, IsSelected = false },
                new() { Quality = StreamQuality.Low, Label = "480p (SD)", Bitrate = 2500, IsSelected = false }
            };

            QualityItems.ItemsSource = _qualityOptions;
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            foreach (var opt in _qualityOptions)
            {
                opt.IsSelected = opt.Quality == _currentQuality;
            }

            // Update auto button
            AutoCheck.Visibility = _currentQuality == StreamQuality.Auto ? Visibility.Visible : Visibility.Collapsed;

            // Refresh display
            QualityItems.ItemsSource = null;
            QualityItems.ItemsSource = _qualityOptions;
        }

        private void QualityOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is QualityDisplayItem item)
            {
                _currentQuality = item.Quality;
                UpdateSelection();
                OnQualitySelected?.Invoke(item.Quality);
            }
        }

        private void AutoOption_Click(object sender, RoutedEventArgs e)
        {
            _currentQuality = StreamQuality.Auto;
            UpdateSelection();
            OnQualitySelected?.Invoke(StreamQuality.Auto);
        }
    }

    /// <summary>
    /// Display item for quality selector
    /// </summary>
    public class QualityDisplayItem
    {
        public StreamQuality Quality { get; set; }
        public string Label { get; set; } = "";
        public int? Bitrate { get; set; }
        public bool IsSelected { get; set; }

        public string BitrateDisplay => Bitrate.HasValue ? $"{Bitrate / 1000.0:F1} Mbps" : "";
    }
}
