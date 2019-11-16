using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LRMServerSDK.LRMServer;

namespace LRMServerSDK
{
    class MessageHandler
    {
        //internal event 
            

        //internal class LRMClientAuthenticated : EventArgs
        //{
        //    internal string DiscordName { get; set; }
        //    internal 
        //}
        private HttpListener currentListener { get; set; }
        public MessageHandler(HttpListener listener)
        {
            currentListener = listener;
        }
        internal bool recieving { get; set; }
        private Thread RecievingThread;
        public MessageHandler()
        {
            StartRecieve();
        }
        private string jsonfyHTTPData(LRM_HTTPData data)
        {
            return JsonConvert.SerializeObject(data);
        }
        public void StopRecieve()
        {
            recieving = false;
            RecievingThread.Abort();
            if (RecievingThread.IsAlive) { throw new Exception("Recieving thread could not be stopped!"); }
            RecievingThread = null;
        }
        public void StartRecieve()
        {
            recieving = true;
            RecievingThread = new Thread(startrecieving);
            RecievingThread.Start();
        }
        private void startrecieving()
        {  
            while (recieving)
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
            try
            {
                byte[] _buffer = new byte[1024];
                int recieved = 0;
                HttpListenerContext context = currentListener.GetContext();
                HttpListenerRequest request = context.Request;
                System.IO.Stream inputStream;
                try
                {
                    inputStream = request.InputStream;
                    recieved = inputStream.Read(_buffer, 0, Convert.ToInt32(request.ContentLength64));
                }
                catch(Exception ex) { throw new Exception("Could not get the Request Stream!", ex); }
                if (request.HttpMethod == "POST")
                {
                    byte[] databuff = new byte[recieved];
                    Array.Copy(_buffer, databuff, recieved);

                    string text = Encoding.ASCII.GetString(databuff);
                    if (text == "") { return; }
                    //do somthing with the data 
                    LRM_HTTPData data = JsonConvert.DeserializeObject<LRM_HTTPData>(text);

                    string responceString = HandleIncoming(data);
                    
                    HttpListenerResponse response = context.Response;

                    byte[] buffer = Encoding.ASCII.GetBytes(responceString);
                    response.ContentLength64 = buffer.Length;
                    Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                }
                if (request.HttpMethod == "GET")
                {
                    HttpListenerResponse response = context.Response;
                    byte[] buffer = Encoding.ASCII.GetBytes("<p><img src=\"https://static-cdn.jtvnw.net/jtv_user_pictures/62bb4f73-e22e-4a7c-9062-ae2ac0177c2b-profile_image-300x300.png\" alt=\"5Daddy\" width=\"158\" height=\"158\"/></p><p>&nbsp;</p><h2><strong>You shouldent be at this page ?</strong></h2><p><strong>Hello?</strong></p><p>&nbsp;</p>");
                    response.ContentLength64 = buffer.Length;
                    Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error with handling message, returning Server_Error to Request", exc);
            }
        }
        internal string HandleIncoming(LRM_HTTPData data)
        {
            string Returnstring = "";
            return Returnstring;
        }
    }
}
