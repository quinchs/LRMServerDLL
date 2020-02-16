using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LRMServerSDK.LRMServer;

namespace LRMServerSDK
{
    class Packets
    {
        public interface IPacket
        {
            string Header { get; set; }
            Dictionary<object, object> Body { get; set; }
            string Auth { get; set; }
        }
        public static RawPacket GetRawPacket(IPacket p)
        {
            if (p == null) { return null; }
            RawPacket r = new RawPacket();
            r.Body = new Dictionary<object, object>();
            r.Header = p.GetType().Name;
            r.Auth = LRMServer.authToken;
            var pr = p.GetType().GetProperties();
            foreach (var prop in pr)
            {
                if (prop.Name != "Header" && prop.Name != "Body" && prop.Name != "Auth")
                {
                    object val = prop.GetValue(p);
                    r.Body.Add(prop.Name, val);
                }
            }
            return r;
        }
        internal class AuthPacket : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }

            public string ClientAuth { get; set; }
        }
        internal class NewClient : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }

            public FlightSim FlightSim { get; set; }
            public string ClientAuth { get; set; }

        }
        internal class RawPacket : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }
        }
        internal class NewLRMServerPacket : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }

            public string Licence_ID { get; set; }
            public string Licence_Password { get; set; }
            public string Server_Name { get; set; }
            public string Server_Type { get; set; }
            public LRMServer.FlightSim[] Simulator_Types { get; set; }
        }
        internal class ResponcePacket : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }

            public object Result { get; set; }
            public bool IsExecption { get; set; }
            public string Exception { get; set; }
        }
        internal class PingPacket : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }

            public string serverName { get; set; }
            public string serverType { get; set; }
        }
        internal class OffsetPacket : IPacket
        {
            public string Header { get; set; }
            public Dictionary<object, object> Body { get; set; }
            public string Auth { get; set; }
            
            public List<offsettSubscription> OffsetList { get; set; }
        }
       
        /* LRM_HTTPData d = new LRM_HTTPData()
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
        */
        //LRM_HTTPData d = new LRM_HTTPData()
        //{
        //    Header = "New_LRMServer",
        //    Auth = null,
        //    Body = new Dictionary<string, string>()
        //        {
        //            {"Licence_ID", Convert.ToString(LicenseID) },
        //            {"Licence_Password", EncodePW(LicensePW) },
        //            {"Server_Name", lrmServerName },
        //            {"Server_Type", ServerType },
        //            {"Simulator_Types",  fligsims}
        //        }
        //};
    }
}
