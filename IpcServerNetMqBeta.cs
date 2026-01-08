using NetMQ;
using NetMQ.Sockets;
using IpcNetMq;
using System;
using System.Linq;
using System.Text;
using IpcNetMq.IpcNetMqHelpers;
using System.Threading.Tasks;
using IpcNetMqHelpers;
using System.Threading;

namespace IpcNetMq
{
    public class IpcServerNetMqBeta : IIpcServer, IDisposable
    {
        /// <summary>
        /// Static mutex assumes only a single server
        /// </summary>
        private static Mutex ReqRepMutex { get; set; }

        /// <summary>
        /// The friendly name of the Client
        /// </summary>
        public string ServerName { get; set; } = "TestServer";

        /// <summary>
        /// Address of the server
        /// E.g. "tcp://127.0.0.1:5555"
        /// </summary>
        public string ServerAddress { get; set; }

        /// <summary>
        /// The socket we bind to, so Requests can be received, and Replies sent.
        /// </summary>
        private ResponseSocket ServerSocket { get; set; }


        /// <summary>
        /// Track the binding
        /// </summary>
        private bool _isBound;
        private bool disposedValue;

        /// <summary>
        /// The integer that increases for each request sent from this client.
        /// The reply must have the same number.
        /// </summary>
        private int SequenceNbr { get; set; }

        private readonly NetMQRuntime _runtime; // create runtime for async operations

        //private IpcConnectionNetMq? _ipcConnection;

        public IpcServerNetMqBeta()
        {
        }

        public IpcServerNetMqBeta(string serverName, string serverAddress)
        {
            string mutexName = CommunicationHelpers.HashServerAddress(serverName);
            ReqRepMutex = new Mutex(false, mutexName);

            _runtime = new NetMQRuntime();

            SequenceNbr = 0;
            ServerName = serverName;
            ServerAddress = serverAddress;
            _isBound = false;
            ServerSocket = null;
        }

        /// <summary>reply
        /// Create/bind the  socket if not already created.
        /// If it exists, then Unbind/Close it.
        /// Regardless, attempt to bind to it.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool OpenSocket(out string reason)
        {
            reason = "";
            string marker = "Checking socket.";
            try
            {
                if (ServerSocket == null)
                {
                    marker = "Creating new socket";
                    ServerSocket = new ResponseSocket();
                }
                else
                {
                    marker = "Unbinding socket";
                    if (CloseSocket(out reason))
                    {
                        marker = "Closing socket";
                        ServerSocket.Close();
                    }
                }

                ServerSocket.Bind(ServerAddress);
                _isBound = true;
                return true;
            }
            catch (Exception ex)
            {
                reason = $"IPC Connection={this.ServerName}. Marker={marker}. Err={ex.Message}";
                return false;
            }

        }

        /// <summary>
        /// Close the connection by unbinding.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool CloseSocket(out string reason)
        {
            reason = "";
            try
            {
                if ( ServerSocket != null )
                {
                    if (_isBound)
                    {
                        ServerSocket.Unbind(ServerAddress);
                        ServerSocket.Close();
                        ServerSocket.Dispose();
                        NetMQConfig.Cleanup();

                        ServerSocket = null;
                        _isBound = false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = $"IPC Address={this.ServerAddress} cannot close connection. Err={ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Get a request packet and deserialize it. Return the deserialized Packet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IpcPacket GetRequestPacket()
        {
            IpcPacket requestPacket;
            try
            {
                // Assuming the server replies immediately after processing the message
                string requestJson = ServerSocket.ReceiveFrameString();

                // Deserialize the entire message
                requestPacket = JsonHelpers.DeserializeFromJsonString(requestJson);
                return requestPacket;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Receive Request. Name={this.ServerName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Get a request packet from a client and deserialize it. Return the deserialized Packet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<IpcPacket> GetRequestPacketAsync()
        {
            IpcPacket requestPacket = null;
            try
            {
                // Assuming the server replies immediately after processing the message
                var (requestJson,moreFrames) = await ServerSocket.ReceiveFrameStringAsync();
                if (!moreFrames)
                {
                    Logit($"Received Packet: {requestJson}");
                    // Deserialize the entire message
                    requestPacket = JsonHelpers.DeserializeFromJsonString(requestJson);
                    return requestPacket;
                }
                else
                {
                    Logit($"The async receive indicates more frames. We don't support multi-frame messages, so we'll fail this.");
                    return requestPacket;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Receive Request. Name={this.ServerName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Send a serialized packet to the client.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool PutReplyPacket(IpcPacket packet)
        {
            if (packet == null)
                throw new Exception($"Cannot send a null packet");

            string checkMessage = packet.Check();

            if (checkMessage != "OK")
                throw new Exception($"Invalid packet. Reason={checkMessage}");

            try
            {
                string replyJson = JsonHelpers.SerializeToJsonString(packet);
                Logit($"Sending Reply Packet. Data Bytes={replyJson.Length}. Sending...");
                ServerSocket.SendFrame(replyJson);
                Logit($"Sent Reply. Serialized Length={replyJson.Length} bytes. ");

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot SendPacket. Name={this.ServerName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Send a serialized packet to the server in an async fashion.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> PutReplyPacketAsync(IpcPacket packet)
        {
            if (packet == null)
                throw new Exception($"Cannot send a null packet");

            string checkMessage = packet.Check();

            if (checkMessage != "OK")
                throw new Exception($"Invalid packet. Reason={checkMessage}");

            try
            {
                string replyJson = JsonHelpers.SerializeToJsonString(packet);
                Logit($"Sending Packet. Data Bytes={replyJson.Length}. Sending...");
                await Task.Run(() => ServerSocket.SendFrame(replyJson));

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot SendPacket. Name={this.ServerName}. Err={ex.Message}");
            }
        }

        public void Logit(string message)
        {
            Logger.LogIt(message);
        }

        /// <summary>
        /// Use the NetQM runtime to launch an async loop
        /// </summary>
        /// <param name="handleAction"></param>
        public void StartIpcServerAsync(Func<IpcPacket, IpcPacket> handleAction)
        {
            _runtime.Run(RunIpcServerLoopAsync(handleAction));
        }

        /// <summary>
        /// The main loop to service requests for IPC Server, and
        /// then process the action. This is async.
        /// </summary>
        /// <param name="connectionName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task RunIpcServerLoopAsync(Func<IpcPacket, IpcPacket> HandleAction)
        {
            Logit($"Starting IPC server with IPC Address={ServerAddress}");

            try
            {
                string reason = "";
                int bindRetries = 0;

                int maxRetries = 10;
                while (bindRetries < maxRetries)
                {
                    try
                    {
                        bindRetries++;
                        if (this.OpenSocket(out reason))
                        {
                            bindRetries = 0;
                            Logit($"=== NetMQ Server={ServerAddress} now bound to socket. Reading requests...");

                            bool KeepReading = true;
                            while (KeepReading)
                            {
                                try
                                {
                                    ReqRepMutex.WaitOne();

                                    var packet = await GetRequestPacketAsync();
                                    if (packet != null)
                                    {
                                        Logit($"Packet={packet} received. Handling Request...");
                                        IpcPacket replyPacket = HandleAction(packet);
                                        if (replyPacket != null)
                                        {
                                            Logit($"Sending Reply Packet={packet}.");
                                            await PutReplyPacketAsync(replyPacket);
                                        }
                                    }
                                }
                                catch (Exception exRead)
                                {
                                    Logit($"Read Error={exRead.Message}. Closing connection");
                                    KeepReading = false;
                                    if (!CloseSocket(out reason))
                                        Logit($"Error Closing connection. Reason={reason}");
                                }
                                finally
                                {
                                    ReqRepMutex.ReleaseMutex();
                                }

                            } // while keepReading
                        } // connection success
                        else
                        {
                            Logit($"Connection retry failed. Reason={reason}. Retries={++bindRetries}");
                        }

                    }
                    catch (Exception exConn)
                    {
                        Logit($"Server={ServerAddress} Connection failed. Err={exConn.Message}. Retries={++bindRetries}");
                    }

                    CloseSocket(out reason);
                    Logit($"IPC Connection={ServerAddress} disconnected. Server will re-create Connection...");

                } // while retry count doesn't exceed max.
            }
            catch (Exception ex)
            {
                Logit($"Unexpected exception. IPC Server will exit. Connection={ServerAddress} Err={ex.Message}");
            }

        }

        /// <summary>
        /// The main loop to service requests for IPC Server, and
        /// then process the action. This is synchronous.
        /// </summary>
        /// <param name="HandleAction"></param>
        /// <exception cref="Exception"></exception>
        public void RunIpcServerLoop(Func<IpcPacket, IpcPacket> HandleAction)
        {

            Logit($"Starting IPC server with IPC Address={ServerAddress}");

            try
            {

                string reason = "";
                int bindRetries = 0;
                int maxRetries = 10;

                while (bindRetries < maxRetries)
                {
                    try
                    {
                        bindRetries++;
                        if (this.OpenSocket(out reason))
                        {
                            bindRetries = 0;
                            Logit($"Server={ServerAddress} now bound to socket. Reading requests...");

                            bool KeepReading = true;
                            while (KeepReading)
                            {
                                try
                                {
                                    ReqRepMutex.WaitOne();

                                    // Synchronous version of request processing
                                    var packet = GetRequestPacket();
                                    if (packet != null)
                                    {
                                        Logit($"Packet={packet} received. Handling Request...");
                                        IpcPacket replyPacket = HandleAction(packet);
                                        if (replyPacket != null)
                                        {
                                            Logit($"Sending Reply Packet={packet}.");
                                            PutReplyPacket(replyPacket);
                                        }
                                    }
                                }
                                catch (Exception exRead)
                                {
                                    Logit($"Read Error={exRead.Message}. Closing connection");
                                    KeepReading = false;
                                    if (!CloseSocket(out reason))
                                        Logit($"Error Closing connection. Reason={reason}");
                                }
                                finally
                                {
                                    ReqRepMutex.ReleaseMutex();
                                }

                            } // while KeepReading
                        } // if connection success
                        else
                        {
                            Logit($"Connection retry failed. Reason={reason}. Retries={++bindRetries}");
                        }

                    }
                    catch (Exception exConn)
                    {
                        Logit($"Server={ServerAddress} Connection failed. Err={exConn.Message}. Retries={bindRetries}");
                    }

                    CloseSocket(out reason);
                    Logit($"IPC Connection={ServerAddress} disconnected. Server will re-create Connection...");
                } // while retries don't exceed maxRetries
            }
            catch (Exception ex)
            {
                Logit($"Unexpected exception. IPC Server will exit. Connection={ServerAddress} Err={ex.Message}");
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    try
                    {
                        ServerSocket?.Dispose();
                        ServerSocket = null;
                    }
                    catch (Exception ex)
                    {
                        Logit($"Warning: Exception while disposing ServerSocket. Err={ex.Message}");
                    }

                    try
                    {
                        _runtime?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logit($"Warning: Exception while disposing NetMQRuntime. Err={ex.Message}");
                    }

                    try
                    {
                        NetMQConfig.Cleanup();
                    }
                    catch (Exception ex)
                    {
                        Logit($"Warning: Exception while cleaning up NetMQConfig. Err={ex.Message}");
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

