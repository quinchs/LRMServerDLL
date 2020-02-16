using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LRMServerSDK;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using Newtonsoft.Json;
using static LRMServerSDK.LRMServer;
using static LRMServerSDK.Packets;

namespace LRMServerSDK
{
    
    internal class LRMClient 
    {
        internal WebSocket ws { get; set; }

        internal WebSocket LowPriorityOffsetSocket;
        internal WebSocket MediumPriorityOffsetSocket;
        internal WebSocket HighPriorityOffsetSocket;
        internal WebSocket StreamPriorityOffsetSocket;

        internal LRMClient(RawPacket data, WebSocket wbs)
        {
            ws = wbs;
            string clientAuth = data.Auth;
            var d = checkClientAuth(clientAuth).Result;
            if (d) //sub to offsets
            {
                isAuthed = true;
                isConnected = true;

            }
            else
            {
                isAuthed = false;
                isConnected = false;
            }
        }
        internal bool isAuthed = false;

        public bool isConnected = false;
        
        public FlightSim CurrentFlightSim { get; internal set; }

        public event EventHandler<OffsetData> LowPriorityOffsetDataUpdated;

        public event EventHandler<OffsetData> MediumPriorityOffsetDataUpdated;

        public event EventHandler<OffsetData> HighPriorityOffsetDataUpdated;

        public event EventHandler<OffsetData> StreamPriorityOffsetData;

        public class OffsetData : EventArgs
        {
            public string name { get;  internal set; }

            public object Value { get; set; }
        }
        public async Task Disconnect()
        {
            if (isConnected)
            {
                
            }
        }
        internal async Task SendData()
    }
}
