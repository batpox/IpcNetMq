//#define POLL_MAIN
#define SYNC_MAIN

using IpcNetMq;
using IpcNetMq.IpcNetMqHelpers;
using IpcNetMqHelpers;
using NetMQ;


namespace IpcTestServer
{
    /// <summary>
    /// This is a test IPC Server. This is generally what the user must write.
    /// It receives Request packets and send Reply packets.
    /// </summary>
    internal class Program
    {

        private static IpcServerNetMq? serverIpc;

        public static void Main(string[] args)
        {
            AsyncIO.ForceDotNet.Force();

            string ipcAddress = args.Length > 0 ? args[0] : "tcp://127.0.0.1:5555";

            Console.WriteLine($"Test Async IPC Server. The server waits for a request from an IpcNetMQ client.");
            Console.WriteLine($"Upon receipt, it processes according to the 'Action' and sends a Reply.");
            Console.WriteLine($"Test IPC Server: Using IPC Address={ipcAddress}");

            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logFilepath = Path.Combine(docPath, $"IpcServer-NetMq.log");
            Console.WriteLine($"=== Server: Initializing Logger. Output={logFilepath}");

            Logger.Initialize(logFilepath);


            try
            {
#if SYNC_MAIN
               Console.WriteLine("Mode: SYNC_MAIN (blocking loop)");

                // Run the server task directly (no StartNew)
                using (var server = new IpcServerNetMq("TestServer", ipcAddress))
                {
                    server.RunIpcServerLoop(UserActions.HandleAction);
                }

#elif POLL_MAIN
                Console.WriteLine("Mode: POLL_MAIN (non-blocking polling loop)");

                serverIpc = new IpcServerNetMq("TestServer", ipcAddress);

                SimulateStart();    // START

                while (true)
                {
                    SimulateUpdate();  // UPDATE
                }
#else
                throw new InvalidOperationException("Define exactly one of: SYNC_MAIN or POLL_MAIN");
#endif
            }
            catch (Exception ex)
            {
                string serverMsg = serverIpc != null ? $"Server={serverIpc}" : "Server not initialized.";
                Logger.LogIt($"Server Loop failed. {serverMsg} Err={ex.Message}");
            }
        }

        /// <summary>
        /// Simulates Stride Start()
        /// Runs once.
        /// </summary>
        private static void SimulateStart()
        {
            if (serverIpc == null)
                throw new InvalidOperationException("serverIpc not initialized in Start.");

            serverIpc.EnsurePollingReady();
        }

        /// <summary>
        /// Simulates Stride Update()
        /// Called repeatedly by the host.
        /// </summary>
        private static void SimulateUpdate()
        {
            if ( serverIpc == null)
                throw new InvalidOperationException("serverIpc not initialized in Update.");
            
            if (serverIpc.TryGetRequest(out var request))
            {
                var reply = UserActions.HandleAction(request)
                            ?? new IpcPacket
                            {
                                Action = request.Action,
                                Status = "Error",
                                ReplyString = "Handler returned null reply."
                            };

                serverIpc.SendReply(reply);
            }

            // NO timing control here.
            // Console test harness runs flat-out.
            // Stride will control cadence via frame timing.
        }


    }
}
