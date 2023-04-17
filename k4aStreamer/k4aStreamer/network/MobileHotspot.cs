using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace k4aStreamer.network
{
    public class MobileHotspot
    {

        
        public static async Task StartHotSpot()
        {
            var filter = new ConnectionProfileFilter();
            filter.IsWlanConnectionProfile = true;
            var wifiProfiles = NetworkInformation.FindConnectionProfilesAsync(filter).AsTask().Result;
            var connectionProfile = wifiProfiles[0];
            
            var tetheringManager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(connectionProfile);
            var access = tetheringManager.GetCurrentAccessPointConfiguration();
            NetworkOperatorTetheringManager.DisableNoConnectionsTimeout();
            
            try
            {
                access.Ssid = ConfigurationManager.AppSettings.Get("Ssid"); 
                if (access.Ssid.Equals("default"))
                {
                    access.Ssid = Environment.GetEnvironmentVariable("COMPUTERNAME") + "-HILLES";
                }
                access.Band = ConfigurationManager.AppSettings.Get("Band").ToLower().Equals("2.4g")
                    ? TetheringWiFiBand.TwoPointFourGigahertz
                    : TetheringWiFiBand.FiveGigahertz;
            }
            catch (NullReferenceException e)
            {
                Console.WriteLine("Error: fail to read App.config");
            }
            
            await tetheringManager.ConfigureAccessPointAsync(access);
            await tetheringManager.StartTetheringAsync();
        }
    }
}