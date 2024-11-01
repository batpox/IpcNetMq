using IpcNetMq.IpcNetMqHelpers;
using IpcNetMqHelpers;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq
{
    /// <summary>
    /// A client connection object.
    /// Handles the bind/unbind, open/close, and put/get of packets
    /// </summary>
    public class IpcClientNetMq : IIpcClient, IDisposable
    {
        private static Mutex ReqRepMutex { get; set; }

        /// <summary>
        /// The friendly name of the Client
        /// </summary>
        public string ClientName { get; set; } = "DefaultClientName";

        /// <summary>
        /// The client-side uses a Request socket to send requests and receive replies.
        /// </summary>
        private RequestSocket ClientSocket { get; set; }

        /// <summary>
        /// Address of the server
        /// E.g. "tcp://127.0.0.1:5555"
        /// </summary>
        public string ServerAddress { get; set; }

        /// <summary>
        /// Track the binding
        /// </summary>
        private bool _isBound;
        private bool disposedValue;

        /// <summary>
        /// The level of logging
        /// </summary>
        public int LoggingLevel { get; set; }

        /// <summary>
        /// The integer that increases for each request sent from this client.
        /// The response must have the same number.
        /// </summary>
        private int SequenceNbr { get; set; }

        public IpcClientNetMq(string clientName, string serverAddress)
        {
            string mutexName = CommunicationHelpers.HashServerAddress(serverAddress);
            ReqRepMutex = new Mutex(false, mutexName);

            SequenceNbr = 0;
            ClientName = clientName;
            ServerAddress = serverAddress;
            _isBound = false;
            ClientSocket = null;
        }

        /// <summary>
        /// Create/connect the request socket if not already created.
        /// If it exists, then Unbind/Close it.
        /// Regardless, attempt to bind to it.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool OpenConnection(out string reason)
        {
            reason = "";
            string marker = "Checking address socket.";

            if (string.IsNullOrEmpty(ServerAddress))
                throw new Exception($"No Server Address is specified");

            try
            {
                marker = "Checking socket.";
                if (ClientSocket != null)
                {
                    marker = "Checking if socket is disposed.";
                    if (!ClientSocket.IsDisposed)
                        ClientSocket.Disconnect(ServerAddress);
                }

                marker = "Creating new socket and connecting";
                ClientSocket = new RequestSocket();
                ClientSocket.Connect(ServerAddress);


                return true;
            }
            catch (Exception ex)
            {
                reason = $"IPC Client Connection={ClientName}. Marker={marker}. Err={ex.Message}";
                return false;
            }

        }


        /// <summary>
        /// Close the connection, unbind if possible
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool CloseConnection(out string reason)
        {
            reason = "";
            try
            {
                if (ClientSocket != null)
                {
                    ClientSocket.Close();
                    ClientSocket.Dispose();
                    ClientSocket = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = $"IPC Connection={ClientName} Address={ServerAddress} cannot close connection. Err={ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Get a response packet from the Server and deserialize it. Return the deserialized Packet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IpcPacket GetResponsePacket()
        {
            IpcPacket responsePacket;
            try
            {
                // Assuming the server replies immediately after processing the message
                if (!ClientSocket.TryReceiveFrameString(TimeSpan.FromSeconds(5), out var responseJson))
                    throw new Exception($"Could not receive from Server within the timeout");

                // Deserialize the entire message
                responsePacket = JsonHelpers.DeserializeFromJsonString(responseJson);
                return responsePacket;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Receive Response. Name={ClientName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Get the response packet and deserialize it. Return the deserialized Packet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<IpcPacket> GetResponsePacketAsync()
        {
            IpcPacket responsePacket = null;
            try
            {
                // Assuming the server replies immediately after processing the message
                var (responseJson, moreFrames) = await ClientSocket.ReceiveFrameStringAsync();
                if (!moreFrames)
                {
                    // Deserialize the entire message
                    responsePacket = JsonHelpers.DeserializeFromJsonString(responseJson);
                    return responsePacket;
                }
                else
                {
                    throw new Exception($"The async receive indicates more frames. We don't support multi-frame messages, so we'll fail this.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot Receive Request. Name={ClientName}. Err={ex.Message}");
            }
        }

        /// <summary>
        /// Send a serialized packet to the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool PutRequestPacket(IpcPacket packet)
        {
            if (packet == null)
                throw new Exception($"Cannot send a null packet");

            string checkMessage = packet.Check();

            if (checkMessage != "OK")
                throw new Exception($"Invalid packet. Reason={checkMessage}");

            try
            {
                string requestJson = JsonHelpers.SerializeToJsonString(packet);
                Logit($"Sending Packet. Data Bytes={requestJson.Length}. Sending...");
                ClientSocket.SendFrame(requestJson);
                Logit($"Sent Response. Serialized Length={requestJson.Length} bytes. ");

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot SendRequestPacket. Name={ClientName}. Err={ex.Message}");
            }

        }

        /// <summary>
        /// Send a serialized packet to the server in an async fashion.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool PutRequestPacketAsync(IpcPacket packet)
        {
            if (packet == null)
                throw new Exception($"Cannot send a null packet");

            string checkMessage = packet.Check();

            if (checkMessage != "OK")
                throw new Exception($"Invalid packet. Reason={checkMessage}");

            try
            {
                string requestJson = JsonHelpers.SerializeToJsonString(packet);
                Logit($"Sending Packet. Data Bytes={requestJson.Length}. Sending...");
                ClientSocket.SendFrame(requestJson);
                Logit($"Sent Packet. ");

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot SendPacket. Name={ClientName}. Err={ex.Message}");
            }

        }

        /// <summary>
        /// Run the client synchronously
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="serverAddress"></param>
        public static IpcPacket CallIpcMethod(IpcClientNetMq client, IpcPacket requestPacket)
        {
            try
            {
                client.SequenceNbr++;
                requestPacket.SequenceNumber = client.SequenceNbr;

                ReqRepMutex.WaitOne();
                client.PutRequestPacket(requestPacket);

                IpcPacket responsePacket = client.GetResponsePacket();

                LogitStatic(client.LoggingLevel, "Received response:");
                LogitStatic(client.LoggingLevel, responsePacket.RequestString);

                if ((requestPacket.SequenceNumber % 1000) == 0)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.ffff}: Processed SequenceNumber={requestPacket.SequenceNumber}");
                }
                return responsePacket;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error: {ex.Message}");
            }
            finally
            {
                ReqRepMutex.ReleaseMutex();
            }

        }

        /// <summary>
        /// Run the client synchronously
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="serverAddress"></param>
        public IpcPacket CallIpcMethod( IpcPacket requestPacket)
        {
            IpcPacket responsePacket =  CallIpcMethod(this, requestPacket);

            return responsePacket;
        }

        /// <summary>
        /// Doing an IPC REQ-REP asynchronously.
        /// Include the client object and the request packet.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static async Task<IpcPacket> CallIpcMethodAsync(IpcClientNetMq client, IpcPacket requestPacket)
        {
            try
            {
                ReqRepMutex.WaitOne();

                client.SequenceNbr++;
                requestPacket.SequenceNumber = client.SequenceNbr;

                client.PutRequestPacketAsync(requestPacket);
                IpcPacket responsePacket = await client.GetResponsePacketAsync();

                LogitStatic(client.LoggingLevel, "Received response:");
                LogitStatic(client.LoggingLevel, responsePacket.RequestString);

                if ((requestPacket.SequenceNumber % 1000) == 0)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.ffff}: Processed SequenceNumber={requestPacket.SequenceNumber}");
                }
                return responsePacket;
            }
            catch (Exception ex)
            {
                LogitStatic(client.LoggingLevel, $"Error: {ex.Message}");
                return null;
            }
            finally { ReqRepMutex.ReleaseMutex(); }
        }

        /// <summary>
        /// Doing an IPC REQ-REP asynchronously.
        /// Include the client object and the request packet.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task<IpcPacket> CallIpcMethodAsync(IpcPacket requestPacket)
        {
            IpcPacket responsePacket = await CallIpcMethodAsync(this, requestPacket);

            return responsePacket;
        }


        public void Logit(string message)
        {
            if ( LoggingLevel > 0 ) 
                Logger.LogIt(message);
        }
        public static void LogitStatic(int loggingLevel, string message)
        {
            if (loggingLevel > 0)
                Logger.LogIt(message);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                    NetMQConfig.Cleanup();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~IpcClientNetMq()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
