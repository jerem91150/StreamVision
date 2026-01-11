using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamVision.Models;

namespace StreamVision.Services
{
    /// <summary>
    /// DLNA/UPnP casting service for streaming to smart TVs and media devices
    /// </summary>
    public class CastService : IDisposable
    {
        private readonly List<CastDevice> _discoveredDevices = new();
        private UdpClient? _ssdpClient;
        private CancellationTokenSource? _discoveryCts;
        private CastDevice? _connectedDevice;
        private bool _isDiscovering;

        // SSDP constants
        private const string SSDP_ADDR = "239.255.255.250";
        private const int SSDP_PORT = 1900;
        private const string SSDP_SEARCH_MSG = "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "MX: 3\r\n" +
            "ST: urn:schemas-upnp-org:device:MediaRenderer:1\r\n\r\n";

        // Events
        public event Action<CastDevice>? OnDeviceDiscovered;
        public event Action<CastDevice>? OnDeviceConnected;
        public event Action<CastDevice>? OnDeviceDisconnected;
        public event Action<string>? OnStatusChanged;
        public event Action<string>? OnError;

        public IReadOnlyList<CastDevice> DiscoveredDevices => _discoveredDevices.AsReadOnly();
        public CastDevice? ConnectedDevice => _connectedDevice;
        public bool IsConnected => _connectedDevice != null;
        public bool IsDiscovering => _isDiscovering;

        /// <summary>
        /// Start discovering cast devices on the network
        /// </summary>
        public async Task StartDiscoveryAsync(int timeoutSeconds = 10)
        {
            if (_isDiscovering) return;

            _isDiscovering = true;
            _discoveredDevices.Clear();
            _discoveryCts = new CancellationTokenSource();

            OnStatusChanged?.Invoke("Recherche d'appareils...");
            LogCast("Starting device discovery");

            try
            {
                _ssdpClient = new UdpClient();
                _ssdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _ssdpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                _ssdpClient.JoinMulticastGroup(IPAddress.Parse(SSDP_ADDR));

                // Send discovery message
                var searchBytes = Encoding.UTF8.GetBytes(SSDP_SEARCH_MSG);
                var endpoint = new IPEndPoint(IPAddress.Parse(SSDP_ADDR), SSDP_PORT);

                for (int i = 0; i < 3; i++)
                {
                    await _ssdpClient.SendAsync(searchBytes, searchBytes.Length, endpoint);
                    await Task.Delay(500);
                }

                // Listen for responses
                var listenTask = ListenForDevicesAsync(_discoveryCts.Token);

                // Wait for timeout or cancellation
                await Task.WhenAny(
                    listenTask,
                    Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), _discoveryCts.Token)
                );

                OnStatusChanged?.Invoke($"{_discoveredDevices.Count} appareil(s) trouvé(s)");
            }
            catch (Exception ex)
            {
                LogCast($"Discovery error: {ex.Message}");
                OnError?.Invoke($"Erreur de découverte: {ex.Message}");
            }
            finally
            {
                _isDiscovering = false;
                _ssdpClient?.Close();
                _ssdpClient?.Dispose();
                _ssdpClient = null;
            }
        }

        /// <summary>
        /// Stop device discovery
        /// </summary>
        public void StopDiscovery()
        {
            _discoveryCts?.Cancel();
            _isDiscovering = false;
        }

        /// <summary>
        /// Connect to a cast device
        /// </summary>
        public async Task<bool> ConnectAsync(CastDevice device)
        {
            try
            {
                LogCast($"Connecting to {device.Name}...");
                OnStatusChanged?.Invoke($"Connexion à {device.Name}...");

                // For DLNA, we don't maintain a persistent connection
                // We just mark it as the selected device
                _connectedDevice = device;
                device.IsConnected = true;

                OnDeviceConnected?.Invoke(device);
                OnStatusChanged?.Invoke($"Connecté à {device.Name}");
                LogCast($"Connected to {device.Name}");

                return true;
            }
            catch (Exception ex)
            {
                LogCast($"Connection error: {ex.Message}");
                OnError?.Invoke($"Erreur de connexion: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnect from current device
        /// </summary>
        public void Disconnect()
        {
            if (_connectedDevice != null)
            {
                var device = _connectedDevice;
                device.IsConnected = false;
                _connectedDevice = null;

                OnDeviceDisconnected?.Invoke(device);
                OnStatusChanged?.Invoke("Déconnecté");
                LogCast($"Disconnected from {device.Name}");
            }
        }

        /// <summary>
        /// Cast a media URL to the connected device
        /// </summary>
        public async Task<bool> CastMediaAsync(string mediaUrl, string title, string? thumbnailUrl = null)
        {
            if (_connectedDevice == null)
            {
                OnError?.Invoke("Aucun appareil connecté");
                return false;
            }

            try
            {
                LogCast($"Casting {title} to {_connectedDevice.Name}");
                OnStatusChanged?.Invoke($"Diffusion de {title}...");

                // Send DLNA SetAVTransportURI command
                var success = await SendDlnaPlayAsync(_connectedDevice, mediaUrl, title);

                if (success)
                {
                    OnStatusChanged?.Invoke($"Lecture sur {_connectedDevice.Name}");
                    return true;
                }
                else
                {
                    OnError?.Invoke("Échec de la diffusion");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogCast($"Cast error: {ex.Message}");
                OnError?.Invoke($"Erreur de diffusion: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop casting
        /// </summary>
        public async Task StopCastingAsync()
        {
            if (_connectedDevice == null) return;

            try
            {
                await SendDlnaStopAsync(_connectedDevice);
                OnStatusChanged?.Invoke("Lecture arrêtée");
            }
            catch (Exception ex)
            {
                LogCast($"Stop error: {ex.Message}");
            }
        }

        /// <summary>
        /// Pause/Resume casting
        /// </summary>
        public async Task PauseAsync()
        {
            if (_connectedDevice == null) return;

            try
            {
                await SendDlnaPauseAsync(_connectedDevice);
            }
            catch (Exception ex)
            {
                LogCast($"Pause error: {ex.Message}");
            }
        }

        #region Private Methods

        private async Task ListenForDevicesAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _ssdpClient != null)
            {
                try
                {
                    var result = await _ssdpClient.ReceiveAsync();
                    var response = Encoding.UTF8.GetString(result.Buffer);

                    if (response.Contains("MediaRenderer"))
                    {
                        var device = ParseSsdpResponse(response, result.RemoteEndPoint);
                        if (device != null && !_discoveredDevices.Any(d => d.Id == device.Id))
                        {
                            _discoveredDevices.Add(device);
                            OnDeviceDiscovered?.Invoke(device);
                            LogCast($"Discovered: {device.Name} at {device.Address}");
                        }
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private CastDevice? ParseSsdpResponse(string response, IPEndPoint endpoint)
        {
            try
            {
                var device = new CastDevice
                {
                    Address = endpoint.Address.ToString(),
                    Port = endpoint.Port
                };

                // Parse headers
                var lines = response.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                    {
                        device.DescriptionUrl = line.Substring("LOCATION:".Length).Trim();
                    }
                    else if (line.StartsWith("USN:", StringComparison.OrdinalIgnoreCase))
                    {
                        device.Id = line.Substring("USN:".Length).Trim();
                    }
                    else if (line.StartsWith("SERVER:", StringComparison.OrdinalIgnoreCase))
                    {
                        device.ModelName = line.Substring("SERVER:".Length).Trim();
                    }
                }

                // Set a display name (would need to fetch from description URL for real name)
                device.Name = $"Media Renderer ({device.Address})";
                device.DeviceType = CastDeviceType.DLNA;

                return device;
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> SendDlnaPlayAsync(CastDevice device, string mediaUrl, string title)
        {
            // DLNA SOAP request for SetAVTransportURI
            var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:SetAVTransportURI xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
      <InstanceID>0</InstanceID>
      <CurrentURI>{System.Security.SecurityElement.Escape(mediaUrl)}</CurrentURI>
      <CurrentURIMetaData></CurrentURIMetaData>
    </u:SetAVTransportURI>
  </s:Body>
</s:Envelope>";

            // Send to device's AVTransport control URL
            // This is simplified - real implementation needs to parse the device description
            try
            {
                using var client = new System.Net.Http.HttpClient();
                var content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:AVTransport:1#SetAVTransportURI\"");

                // Assume standard DLNA control URL
                var controlUrl = $"http://{device.Address}:49152/upnp/control/AVTransport";
                var response = await client.PostAsync(controlUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    // Now send Play command
                    return await SendDlnaPlayCommandAsync(device);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SendDlnaPlayCommandAsync(CastDevice device)
        {
            var soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:Play xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
      <InstanceID>0</InstanceID>
      <Speed>1</Speed>
    </u:Play>
  </s:Body>
</s:Envelope>";

            try
            {
                using var client = new System.Net.Http.HttpClient();
                var content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:AVTransport:1#Play\"");

                var controlUrl = $"http://{device.Address}:49152/upnp/control/AVTransport";
                var response = await client.PostAsync(controlUrl, content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task SendDlnaStopAsync(CastDevice device)
        {
            var soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:Stop xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
      <InstanceID>0</InstanceID>
    </u:Stop>
  </s:Body>
</s:Envelope>";

            try
            {
                using var client = new System.Net.Http.HttpClient();
                var content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:AVTransport:1#Stop\"");

                var controlUrl = $"http://{device.Address}:49152/upnp/control/AVTransport";
                await client.PostAsync(controlUrl, content);
            }
            catch { }
        }

        private async Task SendDlnaPauseAsync(CastDevice device)
        {
            var soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
  <s:Body>
    <u:Pause xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
      <InstanceID>0</InstanceID>
    </u:Pause>
  </s:Body>
</s:Envelope>";

            try
            {
                using var client = new System.Net.Http.HttpClient();
                var content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:AVTransport:1#Pause\"");

                var controlUrl = $"http://{device.Address}:49152/upnp/control/AVTransport";
                await client.PostAsync(controlUrl, content);
            }
            catch { }
        }

        private static void LogCast(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StreamVision", "cast.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #endregion

        public void Dispose()
        {
            StopDiscovery();
            Disconnect();
            _ssdpClient?.Dispose();
            _discoveryCts?.Dispose();
        }
    }

    /// <summary>
    /// Represents a cast-capable device
    /// </summary>
    public class CastDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; }
        public string? DescriptionUrl { get; set; }
        public string? ModelName { get; set; }
        public string? Manufacturer { get; set; }
        public string? IconUrl { get; set; }
        public CastDeviceType DeviceType { get; set; }
        public bool IsConnected { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Name) ? Address : Name;
    }

    /// <summary>
    /// Type of cast device
    /// </summary>
    public enum CastDeviceType
    {
        DLNA,
        Chromecast,
        AirPlay,
        Unknown
    }
}
