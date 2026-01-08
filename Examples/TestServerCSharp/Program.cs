
# define SYNC_MAIN

using IpcNetMq;
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
        private static readonly NetMQRuntime _runtime = new NetMQRuntime();

#if SYNC_MAIN
        public static void Main(string[] args)
        {
            string ipcAddress = args.Length > 0 ? args[0] : "tcp://127.0.0.1:5555";
            Console.WriteLine($"Test Synchronous IPC Server. The server waits for a request from an IpcNetMQ client.");
            Console.WriteLine($"Upon receipt, it processes according to the 'Action' and sends a Reply.");
            Console.WriteLine($"Test IPC Server: Using IPC Address={ipcAddress}");

            try
            {
                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string logFilepath = Path.Combine(docPath, $"IpcServer-NetMq.log");
                Console.WriteLine($"=== Server: Initializing Logger. Output={logFilepath}");

                Logger.Initialize(logFilepath);

                var server = new IpcServerNetMq("TestServer", ipcAddress);
                server.RunIpcServerLoop(UserActions.HandleAction);

            }
            catch (Exception ex)
            {
                Logger.LogIt($"Server Loop failed. Err={ex.Message}");
            }

        }

#else
        public static void Main(string[] args)
        {
            string ipcAddress = args.Length > 0 ? args[0] : "tcp://127.0.0.1:5555";
            Console.WriteLine($"Test Async IPC Server. The server waits for a request from an IpcNetMQ client.");
            Console.WriteLine($"Upon receipt, it processes according to the 'Action' and sends a Reply.");
            Console.WriteLine($"Test IPC Server: Using IPC Address={ipcAddress}");

            try
            {
                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string logFilepath = Path.Combine(docPath, $"IpcServer-NetMq.log");
                Console.WriteLine($"=== Server: Initializing Logger. Output={logFilepath}");

                Logger.Initialize(logFilepath);

                var server = new IpcServerNetMq("TestServer", ipcAddress);

                using var runtime = new NetMQ.NetMQRuntime();

                // Run the server task directly (no StartNew)
                runtime.Run(server.StartIpcServerAsync(UserActions.HandleAction));
            }
            catch (Exception ex)
            {
                Logger.LogIt($"Server Loop failed. Err={ex.Message}");
            }

        }

#endif


    }
}
