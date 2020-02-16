using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LRMServerSDK.LRMServer;
using static LRMServerSDK.Packets;

namespace LRMServerSDK
{
    class MessageHandler
    {
        internal static LRMServer inst;
        internal class WebsocketMSGHandler
        {
            internal Dictionary<Thread, WebSocket> CurrentWebsockets = new Dictionary<Thread, WebSocket>();

            internal void CloseWebsocket(WebSocket socket)
            {
                if (CurrentWebsockets.Values.Contains(socket))
                {
                    var pair = CurrentWebsockets.FirstOrDefault(x => x.Value == socket);
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                    pair.Key.Abort();
                    CurrentWebsockets.Remove(pair.Key);
                }
                else {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
            }

            internal WebsocketMSGHandler(WebSocket socket)
            {
                Thread t = new Thread(() => RevieveWS(socket));
                t.Start();
                CurrentWebsockets.Add(t, socket);
            }
            internal async void RevieveWS(WebSocket socket)
            {
                while (true)
                {
                    try
                    {
                        await HandleWebSocket(socket);
                    
                    }
                    catch (Exception ex)
                    {
                        inst.logMsg($"Error with webhooks, {ex}, retrying in 5 seconds");
                        Thread.Sleep(5000);
                    }
                }
            }
            internal async Task HandleWebSocket(WebSocket wsContext)
            {
                const int maxMessageSize = 1024;
                byte[] receiveBuffer = new byte[maxMessageSize];
                WebSocket socket = wsContext;

                while (socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept binary frame", CancellationToken.None);
                    }
                    else
                    {
                        int count = receiveResult.Count;

                        while (receiveResult.EndOfMessage == false)
                        {
                            if (count >= maxMessageSize)
                            {
                                string closeMessage = string.Format("Maximum message size: {0} bytes.", maxMessageSize);
                                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                                return;
                            }

                            receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, count, maxMessageSize - count), CancellationToken.None);
                            count += receiveResult.Count;
                        }

                        var receivedString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
                        inst.logMsg($"-=- Recieved new Websocket data -=-\n{receivedString}\n-=- End -=-");
                        //handle the new message
                        var packet = JsonConvert.DeserializeObject<Packets.RawPacket>(receivedString);
                        var msg = await MessageResponce.handleMessage(packet);
                        string cont = JsonConvert.SerializeObject(Packets.GetRawPacket(msg));
                        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(cont)), WebSocketMessageType.Text, true, CancellationToken.None);
                        inst.logMsg($"-=- Sent back a websocket responce -=-\n{msg}\n-=- End -=-");
                    }
                }
            }
        }
        internal class LRMClientAuthenticated : EventArgs
        {
            internal string DiscordName { get; set; }
            internal FlightSim currentFlightSim { get; set; }
        }
        private HttpListener currentListener { get; set; }
        public MessageHandler(HttpListener listener, LRMServer instance)
        {
            currentListener = listener;
            inst = instance;
            StartRecieve();
        }
        internal bool recieving { get; set; }
        private Thread RecievingThread;
        
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
        private async void RecieveCallback()
        {
            try
            {
                byte[] _buffer = new byte[1024];
                int recieved = 0;
                HttpListenerContext context = currentListener.GetContext();
                HttpListenerRequest request = context.Request;
                System.IO.Stream inputStream;
                if (request.IsWebSocketRequest)
                {
                    if (request.Headers.AllKeys.Contains("LRMAuth"))
                    {
                        var k = request.Headers.GetValues("LRMAuth");
                        var d = await checkClientAuth(k.First());
                        if (!d)
                        {
                            HttpListenerWebSocketContext c = await context.AcceptWebSocketAsync(null);
                            await c.WebSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Not Authed client", CancellationToken.None);
                            return;
                        }
                        else
                        {
                            HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                            WebSocket webSocket = webSocketContext.WebSocket;
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Accepted")), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                    else
                    {
                        HttpListenerWebSocketContext c = await context.AcceptWebSocketAsync(null);
                        await c.WebSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Not Authed client", CancellationToken.None);
                        return;
                    }
                }
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

                    string text = Encoding.UTF8.GetString(databuff);
                    if (text == "") { return; }
                    //do somthing with the data 
                    RawPacket data = JsonConvert.DeserializeObject<RawPacket>(text);

                    RawPacket resp = HandleIncoming(data);
                    
                    HttpListenerResponse response = context.Response;

                    byte[] buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resp));
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
        internal RawPacket HandleIncoming(RawPacket data, WebSocket es)
        {
            switch(data.Header)
            {
                case "New_LRMClient":
                    return HandleNewLRMClient(data, es);
                    

            }
            return null;
        }
        internal RawPacket HandleNewLRMClient(RawPacket data, WebSocket ws) //needs to get websocket offset stream, 
        {
            LRMClient c = new LRMClient(data, ws);
            
        }
    }
}
