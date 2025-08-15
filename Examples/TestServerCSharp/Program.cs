
using IpcNetMq;
using IpcNetMqHelpers;
using NetMQ;

namespace IpcTestServer
{
    /// <summary>
    /// This is a test IPC Server. This is generally what the user must write.
    /// It receives Request packets and sends Response packets.
    /// </summary>
    internal class Program
    {
        private static readonly NetMQRuntime _runtime = new NetMQRuntime();

//        public static async Task Main(string[] args)
        public static void Main(string[] args)
        {
            string ipcAddress = args.Length > 0 ? args[0] : "tcp://127.0.0.1:5555";
            Console.WriteLine($"Test IPC Server: Using IPC Address={ipcAddress}");

            try
            {
                var runtime = new NetMQ.NetMQRuntime();

                var task = Task.Run( () => 
                {
                    Console.WriteLine("NetMQRuntime is working.");
                    return Task.CompletedTask;
                });

                runtime.Run(task);

                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string logFilepath = Path.Combine(docPath, $"IpcServer-NetMq.log");
                Console.WriteLine($"=== Server: Initializing Logger. Output={logFilepath}");

                Logger.Initialize(logFilepath);

                var server = new IpcServerNetMqBeta("TestServer", ipcAddress);

                bool useAsync = false;
                if (!useAsync)
                {
                    server.StartIpcServerAsync(UserActions.HandleAction);
                }
                else
                {
                    server.RunIpcServerLoop(UserActions.HandleAction);
                }

            }
            catch (Exception ex)
            {
                Logger.LogIt($"Server Loop failed. Err={ex.Message}");
            }

        }
    }
}
