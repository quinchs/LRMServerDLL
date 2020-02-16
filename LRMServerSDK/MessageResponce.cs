using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LRMServerSDK.Packets;

namespace LRMServerSDK
{
    class MessageResponce
    {
        internal static async Task<IPacket> handleMessage(IPacket p)
        {
            switch (p.Header)
            {
                case "ping":
                    return ping(p);
            }
            return null;
        }
        static private IPacket ping(IPacket recieved)
        {
            //string fligsims = string.Join("|", LRMServer.LoadedConfig.allowedFlightSims);
            PingPacket r = new PingPacket()
            {
                serverName = LRMServer.LoadedConfig.LRMServerName,
                serverType = LRMServer.LoadedConfig.ServerType,
            };
            return r;
        }
    }
}
