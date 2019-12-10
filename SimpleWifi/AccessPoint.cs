﻿using SimpleWifi.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SimpleWifi.Win32.Interop;

namespace SimpleWifi
{
	public class AccessPoint
	{
		private WlanInterface _interface;
		private WlanAvailableNetwork _network;
	    private bool _isSsidBroadcasted;

        public AccessPoint(WlanInterface interfac, WlanAvailableNetwork network, bool ssidBroadcasted = false)
		{
			_interface = interfac;
			_network = network;
		}

		public string Name
		{
			get
			{
				return Encoding.UTF8.GetString(_network.dot11Ssid.SSID, 0, (int)_network.dot11Ssid.SSIDLength);
			}
		}

		public uint SignalStrength
		{
			get
			{
				return _network.wlanSignalQuality;
			}
		}

		/// <summary>
		/// If the computer has a connection profile stored for this access point
		/// </summary>
		public bool HasProfile
		{
			get
			{
				try
				{
					return _interface.GetProfiles().Where(p => p.profileName == Name).Any();
				}
				catch 
				{ 
					return false; 
				}
			}
		}
		
		public bool IsSecure
		{
			get
			{
				return _network.securityEnabled;
			}
		}

		public bool IsConnected
		{
			get
			{
				try
				{
					var a = _interface.CurrentConnection; // This prop throws exception if not connected, which forces me to this try catch. Refactor plix.
					return a.profileName == _network.profileName && a.isState == WlanInterfaceState.Connected;
				}
				catch
				{
					return false;
				}
			}

		}

        public string SecurityMethod
        {
            get
            {
                string authAlgo = "Unknown";
                switch(_network.dot11DefaultAuthAlgorithm)
                {
                    case Dot11AuthAlgorithm.IEEE80211_Open:
                        authAlgo = "Open";  break;
                    case Dot11AuthAlgorithm.IEEE80211_SharedKey:
                        authAlgo = "WEP"; break;
                    case Dot11AuthAlgorithm.RSNA:
                        authAlgo = "WPA2-Enterprise"; break;
                    case Dot11AuthAlgorithm.RSNA_PSK:
                        authAlgo = "WPA2-Personal"; break;
                    case Dot11AuthAlgorithm.WPA:
                        authAlgo = "WPA"; break;
                    case Dot11AuthAlgorithm.WPA_None:
                        authAlgo = "WPA-None (Unsupported)"; break;
                    case Dot11AuthAlgorithm.WPA_PSK:
                        authAlgo = "WPA-Personal"; break;
                }
                if(_network.dot11DefaultAuthAlgorithm >= Dot11AuthAlgorithm.IHV_Start && _network.dot11DefaultAuthAlgorithm <= Dot11AuthAlgorithm.IHV_End)
                    authAlgo = "IHV" + ((int)_network.dot11DefaultAuthAlgorithm).ToString();

                string authCipher = "Unknown";
                switch (_network.dot11DefaultCipherAlgorithm)
                {
                    case Dot11CipherAlgorithm.CCMP:
                        authCipher = "AES"; break;
                    case Dot11CipherAlgorithm.None:
                        authCipher = "None"; break;
                    case Dot11CipherAlgorithm.RSN_UseGroup:
                        authCipher = "WPA Group"; break;
                    case Dot11CipherAlgorithm.TKIP:
                        authCipher = "TKIP"; break;
                    case Dot11CipherAlgorithm.WEP:
                        authCipher = "WEP"; break;
                    case Dot11CipherAlgorithm.WEP104:
                        authCipher = "WEP104"; break;
                    case Dot11CipherAlgorithm.WEP40:
                        authCipher = "WEP40"; break;
                    //case Dot11CipherAlgorithm.WPA_UseGroup:
                    //    authCipher = "WPA Group"; break;
                }
                if (_network.dot11DefaultCipherAlgorithm >= Dot11CipherAlgorithm.IHV_Start && _network.dot11DefaultCipherAlgorithm <= Dot11CipherAlgorithm.IHV_End)
                    authCipher = "IHV" + ((int)_network.dot11DefaultCipherAlgorithm).ToString();

                return string.Format("{0} ({1})", authAlgo, authCipher);
            }
        }

        

		/// <summary>
		/// Returns the underlying network object.
		/// </summary>
		public WlanAvailableNetwork Network
		{
			get
			{
				return _network;
			}
		}


		/// <summary>
		/// Returns the underlying interface object.
		/// </summary>
		public WlanInterface Interface
		{
			get
			{
				return _interface;
			}
		}

	    internal bool IsSsidBroadcasted
	    {
	        get { return _isSsidBroadcasted; }
	    }

		/// <summary>
		/// Checks that the password format matches this access point's encryption method.
		/// </summary>
		public bool IsValidPassword(string password)
		{
			return PasswordHelper.IsValid(password, _network.dot11DefaultCipherAlgorithm);
		}		
		
		/// <summary>
		/// Connect synchronous to the access point.
		/// </summary>
		public bool Connect(AuthRequest request, bool overwriteProfile = false)
		{
			// No point to continue with the connect if the password is not valid if overwrite is true or profile is missing.
			if (!request.IsPasswordValid && (!HasProfile || overwriteProfile))
				return false;

			// If we should create or overwrite the profile, do so.
			if (!HasProfile || overwriteProfile)
			{				
				if (HasProfile)
					_interface.DeleteProfile(Name);

				request.Process();				
			}


			// TODO: Auth algorithm: IEEE80211_Open + Cipher algorithm: None throws an error.
			// Probably due to connectionmode profile + no profile exist, cant figure out how to solve it though.
			return _interface.ConnectSynchronously(WlanConnectionMode.Profile, _network.dot11BssType, Name, 6000);			
		}

		/// <summary>
		/// Connect asynchronous to the access point.
		/// </summary>
		public void ConnectAsync(AuthRequest request, bool overwriteProfile = false, Action<bool> onConnectComplete = null)
		{
			// TODO: Refactor -> Use async connect in wlaninterface.
			ThreadPool.QueueUserWorkItem(new WaitCallback((o) => {
				bool success = false;

				try
				{
					success = Connect(request, overwriteProfile);
				}
				catch (Win32Exception)
				{					
					success = false;
				}

				if (onConnectComplete != null)
					onConnectComplete(success);
			}));
		}
				
		public string GetProfileXML()
		{
			if (HasProfile)
				return _interface.GetProfileXml(Name);
			else
				return string.Empty;
		}

		public void DeleteProfile()
		{
			try
			{
				if (HasProfile)
					_interface.DeleteProfile(Name);
			}
			catch { }
		}

		public override sealed string ToString()
		{
			StringBuilder info = new StringBuilder();
			info.AppendLine("Interface: " + _interface.InterfaceName);
			info.AppendLine("Auth algorithm: " + _network.dot11DefaultAuthAlgorithm);
			info.AppendLine("Cipher algorithm: " + _network.dot11DefaultCipherAlgorithm);
			info.AppendLine("BSS type: " + _network.dot11BssType);
			info.AppendLine("Connectable: " + _network.networkConnectable);
			
			if (!_network.networkConnectable)
				info.AppendLine("Reason to false: " + _network.wlanNotConnectableReason);

			return info.ToString();
		}
	}
}
