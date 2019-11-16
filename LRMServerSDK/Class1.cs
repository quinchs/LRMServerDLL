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
        /// <param name="config">The Config class, contains your server details</param>
        public LRMServer(Config config)
        {
            if(config.allowedFlightSims.Length >=1 && config.licenseID != 0)
            {
                LoadedConfig = config;
            }
            else
                throw new Exception("Config is not filled out or invalad config provided");
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
        /// The schema for communication data, all communication data is in this format
        /// </summary>
        public struct LRM_HTTPData
        {
            public string Header { get; set; }
            public Dictionary<string, string> Body { get; set; }
            public string Auth { get; set; }
        }
        public class Config
        {
            public ulong licenseID { get; set; }
            public string licensePW { get; set; }
            public string ServerAddress { get; set; }
            public string LRMServerName { get; set; }
            public string ServerType { get; set; }
            public FlightSim[] allowedFlightSims { get; set; }
        }
        /// <summary>
        /// The Config of the Server
        /// </summary>
        public Config LoadedConfig { get; set; }
        private HttpListener LRMServerListener { get; set; }
        /// <summary>
        /// Connected T/F bool
        /// </summary>
        internal bool isConnected { get; private set; }
        /// <summary>
        /// Auth T/F bool
        /// </summary>
        internal bool isAuthorized { get; private set; }
        /// <summary>
        /// True if the server is running and accepting clients
        /// </summary>
        internal bool ServerRunning { get; private set; }
        /// <summary>
        /// Number of connected clients
        /// </summary>
        public int ConnectedPlayers { get; private set; }

        private string authToken { get; set; }

        private const string MasterServerAddress = "localhost:8080";
        /// <summary>
        /// Connects and authenticate with the master server
        /// </summary>
        public async Task Connect()
        {
            var LicenseID = LoadedConfig.licenseID;
            var LicensePW = LoadedConfig.licensePW;
            var AllowedFlightSims = LoadedConfig.allowedFlightSims;
            var lrmServerName = LoadedConfig.LRMServerName;
            var ServerType = LoadedConfig.ServerType;

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
        public void StartupServer()
        {
            LRMServerListener = new HttpListener();
            if (!HttpListener.IsSupported) { throw new Exception("Your machine does not Support an HttpListener!"); }
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/inbound/");
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/outbound/");
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/authorize/");
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/");
            MessageHandler m = new MessageHandler(LRMServerListener);
            m.StartRecieve();

        }
        private void logMsg(string msg)
        {
            LogMessage.Invoke(this, new LogMessageEventArgs() { Message = msg });
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
