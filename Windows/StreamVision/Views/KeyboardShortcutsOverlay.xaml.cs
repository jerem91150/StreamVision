using System.Windows;
using System.Windows.Input;

namespace StreamVision.Views
{
    /// <summary>
    /// Overlay showing all keyboard shortcuts
    /// </summary>
    public partial class KeyboardShortcutsOverlay : Window
    {
        public KeyboardShortcutsOverlay()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Close on any key press
            Close();
            e.Handled = true;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close on click
            Close();
        }

        /// <summary>
        /// Show the overlay as a modal dialog
        /// </summary>
        public static void ShowOverlay(Window owner)
        {
            var overlay = new KeyboardShortcutsOverlay
            {
                Owner = owner
            };
            overlay.ShowDialog();
        }
    }
}
