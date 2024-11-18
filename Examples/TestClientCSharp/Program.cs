using Newtonsoft.Json;
using IpcNetMq;
using IpcNetMqHelpers;
using NetMQ;
using System.Threading;
using NetMQ.Sockets;
using System.Net;
using System.ServiceModel;
using IpcNetMq.IpcNetMqHelpers;

namespace TestIpcClient
{
    /// <summary>
    /// This is an example/test client. It connects to a NetMq server at ServerAddress and sends Request packets.
    /// 
    /// </summary>
    public class Program
    {
 //       public static async Task Main(string[] args)
        public static void Main(string[] args)
        {
            string serverAddress = args.Length > 0 ? args[0] : "tcp://127.0.0.1:5555";
            string clientName = "TestIpcClient";
            Console.WriteLine($"Client={clientName}: Using Server address={serverAddress}");

            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logFilepath = Path.Combine(docPath, $"IpcClient-{clientName}.log");
            Console.WriteLine($"Client: Initializing Logger. Output={clientName}");

            Logger.Initialize(logFilepath);
            Logger.LogIt($"Run the Client workflow using the NetMQ runtime. Client={clientName}");

            bool useAsync = false;
            if (useAsync)
            {
                using (var runtime = new NetMQRuntime())
                {
                    //runtime.Run(RunClientTestAsync(clientName, serverAddress));
                }
            }
            else
            {
                RunClientTest(clientName, serverAddress);
            }
        }

        /// <summary>
        /// An example of how to create a client and invoke the "CallIpcMethod"
        /// </summary>
        /// <param name="clientName">Client friendly name. Used in logging.</param>
        /// <param name="serverAddress">The location of the Ipc server</param>
        private static void RunClientTest(string clientName, string serverAddress)
        {
            using (var client = new IpcClientNetMq(clientName, serverAddress))
            {
                int delay = 1000; // starting delay, in milliseconds
                int maxDelay = delay * 60;
                int retries = 0;

                string connectionReason = "";
                while (true)
                {
                    try
                    {
                        Logit($"Attempting connect (Retries={retries}) Client={client.ClientName} to server address={serverAddress}...");
                        bool isOpened = client.OpenConnection(out connectionReason);
                        if (isOpened)
                        {
                            Logit("Connected to the server.");
                            retries = 0;
                        }
                        else
                            retries++;

                        try
                        {
                            double simTime = 0.0;
                            while (true)
                            {
                                simTime += 0.1;

                                IpcPacket requestPacket;
                                // Client builds the request here.
                                if ( true ) // Example of literal build
                                {
                                    requestPacket = new IpcPacket
                                    {
                                        SequenceNumber = 0,
                                        Action = "do_get1",
                                        ContextString = JsonHelpers.BuildNameValuePairs(("SimTime", $"{simTime:0.0}")),
                                        ResponseString = JsonHelpers.BuildNameValuePairs(("Result1", ""), ("Result2", "")),
                                        RequestString = JsonHelpers.BuildNameValuePairs(("Value1", "10"), ("Value2", "23.4")),
                                    };

                                }
                                else // Example of programmatic build
                                {
                                    List<NameValuePair> pairs = new List<NameValuePair>();
                                    pairs.Add(new NameValuePair("SimTime", $"{simTime}"));
                                    string contextString = JsonHelpers.SerializePairListToJsonString(pairs);
                                    
                                }

                                IpcPacket responsePacket = client.CallIpcMethod(requestPacket);
                                if (responsePacket != null)
                                {
                                    // Client processes the result here
                                    Logit($"Response={responsePacket}");
                                }
                            } // while making calls
                        }
                        catch (Exception ex)
                        {
                            Logit($"Exception during conversation. Err={ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logit($"Error={ex.Message}");
                    }
                    client.CloseConnection(out connectionReason);
                } // while connecting
            } // created client
        } // RunClientTest

        private static void Logit(string message)
        {
            Logger.LogIt(message);
            message = $"[{DateTime.Now:HH:mm:ss.fff}]: {message}";
            Console.WriteLine(message);

        }

    }

}
