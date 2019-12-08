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
using System.Net.WebSockets;
using System.Threading;

namespace LRMServerSDK
{
    /// <summary>
    /// Base class for your LRM serevr
    /// </summary>
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
        /// <summary>
        /// LRM server log event, use for debug or fancy consoles :D
        /// </summary>
        public event EventHandler<LogMessageEventArgs> LogMessage;
        /// <summary>
        /// Log Message Event data.
        /// </summary>
        public class LogMessageEventArgs : EventArgs { public string Message { get; set; } }
        /// <summary>
        /// User connected event
        /// </summary>
        public event EventHandler UserConnected;
        /// <summary>
        /// User Disconnect event
        /// </summary>
        public event EventHandler UserDisconnected;
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

        internal struct OffsetHTTP
        {
            /// <summary>
            /// Header of the message
            /// </summary>
            public string Header { get; set; }
            /// <summary>
            /// Body of the message, contains info regarding the Header
            /// </summary>
            public List<offsettSubscription> OffsetList { get; set; }
            /// <summary>
            /// Auth token of the LRM Server
            /// </summary>
            public string Auth { get; set; }
        }

        /// <summary>
        /// The schema for communication data, all communication data is in this format
        /// </summary>
        public struct LRM_HTTPData
        {
            /// <summary>
            /// Header of the message
            /// </summary>
            public string Header { get; set; }
            /// <summary>
            /// Body of the message, contains info regarding the Header
            /// </summary>
            public Dictionary<string, string> Body { get; set; }
            /// <summary>
            /// Auth token of the LRM Server
            /// </summary>
            public string Auth { get; set; }
        }
        /// <summary>
        /// Your LRM server configuration Settings
        /// </summary>
        public class Config
        {
            /// <summary>
            /// Initializes a new Config class
            /// </summary>
            /// <param name="licenseID">Your Licence ID</param>
            /// <param name="licensePW">Your Licence Password</param>
            /// <param name="ServerAddress">Your server BASE Address, ex: https://yourDomain.com/LRM</param>
            /// <param name="LRMServerName">Your LRM Server name</param>
            /// <param name="ServerType">Your Server type/genre. ex: Landing Compitition </param>
            /// <param name="allowedFlightSims">Allowed flight simulators for your server, if you want only FSX users to connect to your server you can specify that here</param>
            public Config(ulong licenseID, string licensePW, string ServerAddress, string LRMServerName, string ServerType, params FlightSim[] allowedFlightSims)
            {
                this.allowedFlightSims = allowedFlightSims;
                this.licenseID = licenseID;
                this.licensePW = licensePW;
                this.LRMServerName = LRMServerName;
                this.ServerType = ServerType;
                this.ServerAddress = ServerAddress;
            }
            /// <summary>
            /// Your Licence ID
            /// </summary>
            public ulong licenseID { get; set; }
            /// <summary>
            /// Your Licence Password
            /// </summary>
            public string licensePW { get; set; }
            /// <summary>
            /// Your server BASE Address DO NOT add a / (slash) at the end, ex: https://yourdomain.com/lrm
            /// </summary>
            public string ServerAddress { get; set; }
            /// <summary>
            /// Your LRM Server name
            /// </summary>
            public string LRMServerName { get; set; }
            /// <summary>
            /// Your Server type/genre. ex: Landing Compitition 
            /// </summary>
            public string ServerType { get; set; }
            /// <summary>
            /// Allowed flight simulators for your server, if you want only FSX users to connect to your server you can specify that here
            /// </summary>
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
        public bool isConnected { get; private set; }
        /// <summary>
        /// Auth T/F bool
        /// </summary>
        public bool isAuthorized { get; private set; }
        /// <summary>
        /// True if the server is running and accepting clients
        /// </summary>
        public bool ServerRunning { get; private set; }
        /// <summary>
        /// Number of connected clients
        /// </summary>
        public int ConnectedPlayers { get; private set; }

        internal string authToken { get; set; }

        internal List<offsettSubscription> offsets { get; set; }

        private const string MasterServerAddress = "http://localhost:8080/";
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
                authToken = resp.Auth;
                logMsg($"Authorized by master server!");

            }
            await ExchangeWebsocket();
        }
        internal async Task ExchangeWebsocket()
        {
            ClientWebSocket c = new ClientWebSocket();
            await c.ConnectAsync(new Uri(MasterServerAddress), CancellationToken.None);
            if(c.State == WebSocketState.Open)
            {
                MessageHandler.WebsocketMSGHandler h = new MessageHandler.WebsocketMSGHandler(c);
            }

        }
        /// <summary>
        /// Starts up your LRM server.
        /// </summary>
        /// <param name="offsetSubscriptions">The offsets you want to recieve from LRM Clients, Max is 10 offsets</param>
        public async void StartupServer(List<offsettSubscription> offsetSubscriptions)
        {
            if(offsetSubscriptions.Count > 10) { throw new Exception("Cant have more than 10 offsets!"); }
            offsets = offsetSubscriptions;
            LRMServerListener = new HttpListener();
            if (!HttpListener.IsSupported) { throw new Exception("Your machine does not Support an HttpListener!"); }
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/inbound/");
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/outbound/");
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/authorize/");
            LRMServerListener.Prefixes.Add($"{LoadedConfig.ServerAddress}/lrm/");
            OffsetHTTP data = new OffsetHTTP()
            {
                Header = "offsets",
                Auth = authToken,
                OffsetList = offsets
            };
            var d = await SendandRecieveHTTPData(data);
            if (isError(d))
                throw new Exception($"Server error!, {d.Header}");
            MessageHandler m = new MessageHandler(LRMServerListener,this);
            m.StartRecieve();
            
        }
        /// <summary>
        /// Details about the offset data you want to revieve from LRM Clients
        /// </summary>
        public struct offsettSubscription
        {
            /// <summary>
            /// Name of the offset (doesnt need to be the acual name, can be what you want). when the OffsetData event triggered it will have the name you specify here
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// the Address of the offset. EX: 0x11BA is the gForce offset.
            /// </summary>
            public int offsetAdress { get; set; }
            /// <summary>
            /// The type of data the offset is, ex 0x11BA is gForce and its data is read as an int32
            /// </summary>
            public DataType DataType { get; set; }
            /// <summary>
            /// Tells the LRM Client how often it should send the offset value
            /// </summary>
            public Priority offsetPriority { get; set; }
        
        }
        /// <summary>
        /// The enum containing the Priority level
        /// </summary>
        public enum Priority
        {
            /// <summary>
            /// Sends the Offset value every 5 seconds
            /// </summary>
            Low = 5000,
            /// <summary>
            /// Sends the offset value every 2.5 seconds
            /// </summary>
            Medium = 2500,
            /// <summary>
            /// Sends the offset value every 1 seconds
            /// </summary>
            High = 1000,
            /// <summary>
            /// a constant stream of the offset
            /// </summary>
            Constant = 10,
        }
        /// <summary>
        /// The type of data the Offset is,
        /// </summary>
        public enum DataType
        {
            Byte,
            SByte,
            int16,
            uint16,
            int32,
            uint32,
            int64,
            uint64,
            Double,
            single,
            String,
            bitArray
        }
        internal bool isError(LRM_HTTPData data)
        {
            if (data.Header.ToLower().Contains("error"))
                return true;
            return false;
        }
        internal void logMsg(string msg)
        {
            LogMessage?.Invoke(null, new LogMessageEventArgs() { Message = msg });
        }
        private string EncodePW(string password)
        {
            string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(password));
            SHA512 hsh = new SHA512Managed();
            return Encoding.ASCII.GetString(hsh.ComputeHash(Encoding.ASCII.GetBytes(b64)));
        }
        internal async Task<string> SendandRecieveJson(object data)
        {
            try
            {
                HttpClient c = new HttpClient();
                string json = JsonConvert.SerializeObject(data);
                logMsg($"Sending the json: {json}");
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
                c.Dispose();
                return text;
               
            }
            catch (Exception ex)
            {
                throw new Exception("Could Not Send Data to Master Server!", ex);
            }
        }

        internal async Task<LRM_HTTPData> SendandRecieveHTTPData(object data)
        {
            try
            {
                HttpClient c = new HttpClient();
                string json = JsonConvert.SerializeObject(data);
                logMsg($"Sending the json: {json}");
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
                c.Dispose();
                return JsonConvert.DeserializeObject<LRM_HTTPData>(text);
            }
            catch (Exception ex)
            {
                throw new Exception("Could Not Send Data to Master Server!", ex);
            }
        }
    }
}
