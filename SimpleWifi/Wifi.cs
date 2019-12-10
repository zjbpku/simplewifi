using SimpleWifi.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SimpleWifi.Win32.Interop;

using NotifCodeACM = SimpleWifi.Win32.Interop.WlanNotificationCodeAcm;
using NotifCodeMSM = SimpleWifi.Win32.Interop.WlanNotificationCodeMsm;

namespace SimpleWifi
{
	public class Wifi
	{
		public event EventHandler<WifiStatusEventArgs> ConnectionStatusChanged;
		public event EventHandler<WlanNotificationEventArgs> WirelessNotification;

		private WlanClient _client;
		private WifiStatus _connectionStatus;
        private bool _isConnectionStatusSet = false;
        public bool NoWifiAvailable = false;

        private DateTime _lastScanned = DateTime.MinValue;

		public Wifi()
		{
			_client = new WlanClient();
            NoWifiAvailable = _client.NoWifiAvailable;
            if (_client.NoWifiAvailable)
                return;
			
			foreach (var inte in _client.Interfaces)
				inte.WlanNotification += inte_WlanNotification;

            // Scan  all interfaces
            Scan();
		}

        /// <summary>
        /// Scann all interfaces
        /// </summary>
        public void InterfacesScan()
        {
            foreach (WlanInterface wlanIface in _client.Interfaces)
            {
                wlanIface.Scan();
            }
        }		

        /// <summary>
        /// Returns count Wi-Fi Interfaces
        /// </summary>
        public int InterfacesCount()
        {
            return _client.Interfaces.Length;
        }

        /// <summary>
        /// Returns Interfaces
        /// </summary>
        public WlanInterface[] Interfaces()
        {
            return _client.Interfaces;
        }

        /// <summary>
        /// Returns the underlying WlanClient
        /// </summary>
        public WlanClient Client { get { return _client; } }

        /// <summary>
        /// Returns a list over all available access points
        /// </summary>
		public List<AccessPoint> GetAccessPoints(bool bRescan = true)
		{
            List<AccessPoint> accessPoints = new List<AccessPoint>();
            if (_client.NoWifiAvailable)
                return accessPoints;

            if (bRescan && (DateTime.Now - _lastScanned > TimeSpan.FromSeconds(60)))
                Scan();

			foreach (WlanInterface wlanIface in _client.Interfaces)
			{
				WlanAvailableNetwork[] rawNetworks = wlanIface.GetAvailableNetworkList(0);
				List<WlanAvailableNetwork> networks = new List<WlanAvailableNetwork>();

				// Remove network entries without profile name if one exist with a profile name.
				foreach (WlanAvailableNetwork network in rawNetworks)
				{
					bool hasProfileName						= !string.IsNullOrEmpty(network.profileName);
					bool anotherInstanceWithProfileExists	= rawNetworks.Where(n => n.Equals(network) && !string.IsNullOrEmpty(n.profileName)).Any();

					if (!anotherInstanceWithProfileExists || hasProfileName)
						networks.Add(network);
				}

				foreach (WlanAvailableNetwork network in networks)
				{
					accessPoints.Add(new AccessPoint(wlanIface, network));
				}
			}

			return accessPoints;
		}

		/// <summary>
		/// Rescan all wifi interfaces
		/// </summary>
        public void Scan()
        {
            foreach (WlanInterface wlanIface in _client.Interfaces)
            {
                try
                {
                    wlanIface.Scan();
                }
                catch { }
            }
            _lastScanned = DateTime.Now;
        }

		/// <summary>
		/// Disconnect all wifi interfaces
		/// </summary>
		public void Disconnect()
        {
            if (_client.NoWifiAvailable)
                return;

			foreach (WlanInterface wlanIface in _client.Interfaces)
			{
				wlanIface.Disconnect();
			}		
		}
		public WifiStatus ConnectionStatus
		{
			get
			{
				if (!_isConnectionStatusSet)
					ConnectionStatus = GetForcedConnectionStatus();

				return _connectionStatus;
			}
			private set
			{
				_isConnectionStatusSet = true;
				_connectionStatus = value;
			}
		}

		private void inte_WlanNotification(WlanNotificationData notifyData)
		{
            // Push this notification out to our listners
            if (WirelessNotification != null)
                WirelessNotification(this, new WlanNotificationEventArgs(notifyData));

			if (notifyData.notificationSource == WlanNotificationSource.ACM && (NotifCodeACM)notifyData.NotificationCode == NotifCodeACM.Disconnected)
				OnConnectionStatusChanged(WifiStatus.Disconnected);
			else if (notifyData.notificationSource == WlanNotificationSource.MSM && (NotifCodeMSM)notifyData.NotificationCode == NotifCodeMSM.Connected)
				OnConnectionStatusChanged(WifiStatus.Connected);
		}

		private void OnConnectionStatusChanged(WifiStatus newStatus)
		{
			ConnectionStatus = newStatus;

			if (ConnectionStatusChanged != null)
				ConnectionStatusChanged(this, new WifiStatusEventArgs(newStatus));
		}

		// I don't like this method, it's slow, ugly and should be refactored ASAP.
        private WifiStatus GetForcedConnectionStatus()
        {
            if (NoWifiAvailable)
                return WifiStatus.Disconnected;

			bool connected = false;

			foreach (var i in _client.Interfaces)
			{
				try
				{
					var a = i.CurrentConnection; // Current connection throws an exception if disconnected.
					connected = true;
				}
				catch {	}
			}

			if (connected)
				return WifiStatus.Connected;
			else
				return WifiStatus.Disconnected;
		}		
	}

	public class WifiStatusEventArgs : EventArgs
	{
		public WifiStatus NewStatus { get; private set; }

		internal WifiStatusEventArgs(WifiStatus status) : base()
		{
			this.NewStatus = status;
		}

	}

	public enum WifiStatus
	{
		Disconnected,
		Connected
	}

    public class WlanNotificationEventArgs : EventArgs
    {
        public WlanNotificationData eventData { get; private set; }

        internal WlanNotificationEventArgs(WlanNotificationData status) : base()
        {
            this.eventData = status;
        }

    }


}
