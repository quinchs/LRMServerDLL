using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LRMServerSDK
{
    public class LRMServer
    {
        /// <summary>
        /// initialize a new instance of the LRMServer Class
        /// </summary>
        /// <param name="licenseID">Your LRM License ID</param>
        /// <param name="licensePassword">Your LRM License Password</param>
        /// <param name="serverAdress">Your HTTP Server Address, ex http://123.45.567.9.com/ or http://LRMServer.com/</param>
        public LRMServer (ulong licenseID, string licensePassword, string serverAdress)
        {

        }
        public static event EventHandler LogMessage;
        public class LogMessageEventArgs : EventArgs { public string Message { get; set; } }
        public static event EventHandler UserConnected;
        public static event EventHandler UserDisconnected;
        #region FSenum
        //
        // Summary:
        //     Flight Simulator Version
        public enum FlightSim
        {
            //
            // Summary:
            //     Any version of Flight Sim
            Any = 0,
            //
            // Summary:
            //     Microsoft Flight Simulator 98
            FS98 = 1,
            //
            // Summary:
            //     Microsoft Flight Simulator 2000
            FS2K = 2,
            //
            // Summary:
            //     Microsoft Combat Flight Simulator 2
            CFS2 = 3,
            //
            // Summary:
            //     Microsoft Combat Flight Simulator 1
            CFS1 = 4,
            //
            // Summary:
            //     Fly! by Terminal Velocity. (I don't think this works).
            FLY = 5,
            //
            // Summary:
            //     Microsoft Flight Simulator 2002
            FS2K2 = 6,
            //
            // Summary:
            //     Microsoft Flight Simulator 2004 (A Century of Flight)
            FS2K4 = 7,
            //
            // Summary:
            //     Microsoft Flight Simulator X
            FSX = 8,
            //
            // Summary:
            //     Microsoft ESP
            ESP = 9,
            //
            // Summary:
            //     Lockheed Martin - Prepar3D
            Prepar3d = 10,
            //
            // Summary:
            //     Flight Sim World (Reserved)
            FSW = 11,
            //
            // Summary:
            //     Prepar3d Version 4 and above (64 bit)
            Prepar3dx64 = 12
        }
        #endregion FSenum

        /// <summary>
        /// The schema for communications, all communications are in this format
        /// </summary>
        public struct LRM_HTTPData
        {
            public string Header { get; set; }
            public Dictionary<string, string> Body { get; set; }
            public string Auth { get; set; }
        }
       
        private HttpListener LRMServerListener { get; set; }
        internal bool isConnected { get; private set; }
        internal bool isAuthorized { get; private set; }
        internal bool ServerRunning { get; private set; }
        public int ConnectedPlayers { get; private set; }
        private string authToken { get; set; }

        private const string MasterServerAddress = "localhost:8080";
        /// <summary>
        /// Connect and authenticate with the master server
        /// </summary>
        /// <param name="LicenseID">Your LRMServer License ID</param>
        /// <param name="LicensePW">Your LRMServer License Password</param>
        /// <param name="lrmServerName">The LRM Server name you want</param>
        /// <param name="ServerType">The Type of LRM Server, ex: Landing Competition</param>
        /// <param name="AllowedFlightSims">The allowed flightsimulators, ex: P3D</param>
        public async Task Connect(ulong LicenseID, string LicensePW, string lrmServerName, string ServerType, params FlightSim[] AllowedFlightSims)
        {
            string fligsims = string.Join("|", AllowedFlightSims);

            LRM_HTTPData d = new LRM_HTTPData()
            {
                Header = "New_LRMServer",
                Auth = null,
                Body = new Dictionary<string, string>()
                {
                    {"Licence_ID", Convert.ToString(LicenseID) },
                    {"Licence_Password", EncodePW(LicensePW) },
                    {"Server_Name", lrmServerName },
                    {"Server_Type", ServerType },
                    {"Simulator_Types",  fligsims}
                }
            };
            var resp = await SendandRecieveHTTPData(d);
            if (resp.Header == "Server_Error") { throw new Exception($"Recieved Server error!, Reason: {resp.Body.FirstOrDefault(x => x.Key == "Reason").Value}"); }
            if (resp.Header == "Server_Added")
            {
                isConnected = true;
                isAuthorized = true;
                LogMessage.Invoke(null, new LogMessageEventArgs() { Message = $"Authorized by master server!" });
            }
        }
        private async Task StartupServer()
        {
            LRMServerListener = new HttpListener();
            if (!HttpListener.IsSupported) { throw new Exception("Your machine does not Support an HttpListener!"); }
            
        }
        private string EncodePW(string password)
        {
            string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(password));
            SHA512 hsh = new SHA512Managed();
            return Encoding.ASCII.GetString(hsh.ComputeHash(Encoding.ASCII.GetBytes(b64)));
        }
        private async Task<string> SendandRecieveJson(LRM_HTTPData data)
        {
            try
            {
                HttpClient c = new HttpClient();
                string json = JsonConvert.SerializeObject(data);
                LogMessage.Invoke(null, new LogMessageEventArgs() { Message = $"Sending the json: {json}" });

                var req = new HttpRequestMessage()
                {
                    Content = new ByteArrayContent(Encoding.ASCII.GetBytes(json)),
                    Method = new HttpMethod("POST"),
                    RequestUri = new Uri(MasterServerAddress),
                };
                c.Timeout = TimeSpan.FromMilliseconds(5000);
                var msg = c.SendAsync(req).Result;
                Stream rstream = await msg.Content.ReadAsStreamAsync();
                byte[] buff = new byte[1024];
                int rec = rstream.Read(buff, 0, Convert.ToInt32(rstream.Length));
                byte[] databuff = new byte[rec];
                Array.Copy(buff, databuff, rec);
                string text = Encoding.ASCII.GetString(databuff);
                LogMessage.Invoke(null, new LogMessageEventArgs() { Message = $"Recieved new json: {text}" });
                return text;
               
            }
            catch (Exception ex)
            {
                throw new Exception("Could Not Send Data to Master Server!", ex);
            }
        }
        
        private async Task<LRM_HTTPData> SendandRecieveHTTPData(LRM_HTTPData data)
        {
            try
            {
                HttpClient c = new HttpClient();
                string json = JsonConvert.SerializeObject(data);
                LogMessage.Invoke(null, new LogMessageEventArgs() { Message = $"Sending the json: {json}" });
                var req = new HttpRequestMessage()
                {
                    Content = new ByteArrayContent(Encoding.ASCII.GetBytes(json)),
                    Method = new HttpMethod("POST"),
                    RequestUri = new Uri(MasterServerAddress),
                };
                c.Timeout = TimeSpan.FromMilliseconds(5000);
                var msg = c.SendAsync(req).Result;
                Stream rstream = await msg.Content.ReadAsStreamAsync();
                byte[] buff = new byte[1024];
                int rec = rstream.Read(buff, 0, Convert.ToInt32(rstream.Length));
                byte[] databuff = new byte[rec];
                Array.Copy(buff, databuff, rec);
                string text = Encoding.ASCII.GetString(databuff);
                LogMessage.Invoke(null, new LogMessageEventArgs() { Message = $"Recieved new json: {text}" });
                return JsonConvert.DeserializeObject<LRM_HTTPData>(text);
            }
            catch (Exception ex)
            {
                throw new Exception("Could Not Send Data to Master Server!", ex);
            }
        }
    }
}
