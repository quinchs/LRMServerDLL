using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LRMServerSDK.LRMServer;

namespace LRMServerSDK
{
    class MessageHandler
    {
        public MessageHandler()
        {
            StartRecieve();
        }
        private string jsonfyHTTPData(LRM_HTTPData data)
        {
            return JsonConvert.SerializeObject(data);
        }
        private void StartRecieve()
        {
            while (true)
            {
                try
                {
                    RecieveCallback();
                }
                catch (Exception ex)
                {
                    throw new Exception("LRM callback exception", ex);
                }
            }
        }
        private void RecieveCallback()
        {

        }
        internal string HandleIncoming(LRM_HTTPData data)
        {
            string Returnstring = "";
            return Returnstring;
        }
    }
}
