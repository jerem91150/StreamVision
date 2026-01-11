using System.Windows;

namespace StreamVision.Views
{
    public partial class AddPlaylistDialog : Window
    {
        public string SelectedType { get; private set; } = "M3U";
        public string PlaylistName => NameTextBox.Text;
        public string PlaylistUrl => UrlTextBox.Text;
        public string Username => UsernameTextBox.Text;
        public string Password => PasswordBox.Password;
        public string MacAddress => MacTextBox.Text;
        public string EpgUrl => EpgTextBox.Text;

        public AddPlaylistDialog()
        {
            InitializeComponent();
            UpdateHelpText();
        }

        private void PlaylistType_Changed(object sender, RoutedEventArgs e)
        {
            // Vérifier que tous les contrôles sont initialisés (évite NullReferenceException pendant InitializeComponent)
            if (M3URadio == null || UrlLabel == null || UsernamePanel == null ||
                PasswordPanel == null || MacPanel == null) return;

            if (M3URadio.IsChecked == true)
            {
                SelectedType = "M3U";
                UrlLabel.Text = "URL de la playlist";
                UsernamePanel.Visibility = Visibility.Collapsed;
                PasswordPanel.Visibility = Visibility.Collapsed;
                MacPanel.Visibility = Visibility.Collapsed;
            }
            else if (XtreamRadio.IsChecked == true)
            {
                SelectedType = "Xtream";
                UrlLabel.Text = "URL du serveur";
                UsernamePanel.Visibility = Visibility.Visible;
                PasswordPanel.Visibility = Visibility.Visible;
                MacPanel.Visibility = Visibility.Collapsed;
            }
            else if (StalkerRadio.IsChecked == true)
            {
                SelectedType = "Stalker";
                UrlLabel.Text = "URL du portail";
                UsernamePanel.Visibility = Visibility.Collapsed;
                PasswordPanel.Visibility = Visibility.Collapsed;
                MacPanel.Visibility = Visibility.Visible;
            }

            UpdateHelpText();
        }

        private void UpdateHelpText()
        {
            if (HelpText == null) return;

            if (M3URadio?.IsChecked == true)
            {
                HelpText.Text = "Collez l'URL de votre fichier M3U ou M3U8.\nExemple: http://exemple.com/playlist.m3u8";
            }
            else if (XtreamRadio?.IsChecked == true)
            {
                HelpText.Text = "Entrez l'URL du serveur et vos identifiants fournis par votre fournisseur IPTV.\nExemple URL: http://iptv.exemple.com ou http://iptv.exemple.com:8080";
            }
            else if (StalkerRadio?.IsChecked == true)
            {
                HelpText.Text = "Entrez l'URL du portail Stalker et votre adresse MAC.\nExemple MAC: 00:1A:79:XX:XX:XX";
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlaylistUrl))
            {
                MessageBox.Show("Veuillez entrer une URL.", "Champ requis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedType == "Xtream")
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    MessageBox.Show("Veuillez entrer votre nom d'utilisateur.", "Champ requis",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(Password))
                {
                    MessageBox.Show("Veuillez entrer votre mot de passe.", "Champ requis",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (SelectedType == "Stalker" && string.IsNullOrWhiteSpace(MacAddress))
            {
                MessageBox.Show("Veuillez entrer votre adresse MAC.", "Champ requis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Auto-generate name if empty
            if (string.IsNullOrWhiteSpace(PlaylistName))
            {
                NameTextBox.Text = SelectedType switch
                {
                    "M3U" => "Ma playlist M3U",
                    "Xtream" => $"IPTV - {Username}",
                    "Stalker" => $"Stalker - {MacAddress[..8]}...",
                    _ => "Ma playlist"
                };
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
