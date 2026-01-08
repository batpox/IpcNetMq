// Program.cs — TestIpcClient (dispatcher-only usage)
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IpcNetMq;
using IpcNetMqHelpers;          // JsonHelpers, Logger
using IpcNetMq.IpcNetMqHelpers; // NameValuePair if you use it

namespace TestIpcClient
{
    public static class Program
    {
        public static int Main(string[] args)
            => RunAsync(args).GetAwaiter().GetResult();

        private static async Task<int> RunAsync(string[] args)
        {
            // ----------------- Config -----------------
            string serverAddress = (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                ? args[0]
                : "tcp://127.0.0.1:5555";

            string clientName = "TestIpcClient";
            var sendTimeout = TimeSpan.FromSeconds(2);
            var receiveTimeout = TimeSpan.FromSeconds(5);
            int pollIntervalMs = 1;   // how often to send a request
            int reportIntervalPackets = 10_000;   // after how many packets to log
            int backoffMs = 250;   // on error/timeout

            Console.WriteLine( $"Starting IpcNetMQ Client {clientName}, that does Request-Reply (ReqRep) transactions with the TestIpcServer. ");
            Console.WriteLine( $"If the server is not available, it will timeout every {sendTimeout} seconds.");
            Console.WriteLine( $"After every {reportIntervalPackets} transactions, it will report the count and average time for each transaction.");
            Console.WriteLine( $"Client={clientName}. IpcNetMq ServerAddress={serverAddress}");

            // Init logger to Documents
            var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var logFilepath = Path.Combine(docPath, $"IpcClient-{clientName}.log");
            Console.WriteLine(
                  $"Client: Initializing Logger. Output={logFilepath}");
            Logger.Initialize(logFilepath);

            // Ctrl+C support
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Cancel requested…");
            };

            // ----------------- Client (dispatcher) -----------------
            using var client = new IpcClientNetMq(clientName, serverAddress) { LoggingLevel = 1 };
            // NOTE: Do NOT call OpenConnection(); the dispatcher opens/reopens as needed.

            // Optional: log actual DLL loaded to catch stale copies
            try
            {
                var asm = typeof(IpcClientNetMq).Assembly;
                var loc = asm.Location;
                var ver = asm.GetName().Version;
                Logit( $"IpcNetMq loaded from: {loc} AssemblyVersion={ver}");
            }
            catch { /* best effort */ }

            Logit(
                  $"*** Dispatcher client ready. "
                + $"SendTimeout={sendTimeout.TotalMilliseconds}ms, "
                + $"RecvTimeout={receiveTimeout.TotalMilliseconds}ms, "
                + $"Interval={pollIntervalMs}ms");

            // ----------------- Poll loop -----------------
            double simTime = 0.0;
            long packetsSent = 0;
            var sw = Stopwatch.StartNew();

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    simTime += pollIntervalMs / 1000.0;

                    // Build one request (dispatcher will assign SequenceNumber)
                    var request = new IpcPacket
                    {
                        Action = "do_get1", // or "do_getStrokeData" per your server
                        ContextString = JsonHelpers.BuildNameValuePairs(("SimTime", $"{simTime:0.0}")),
                        RequestString = JsonHelpers.BuildNameValuePairs(("Value1", "10"), ("Value2", "23.4")),
                        ReplyString = JsonHelpers.BuildNameValuePairs(("Result1", ""), ("Result2", ""))
                    };

                    var reply =
                        await client.CallIpcMethodAsync(
                            request,
                            sendTimeout: sendTimeout,
                            receiveTimeout: receiveTimeout,
                            ct: cts.Token);

                    packetsSent++;

                    if (reply != null && ((packetsSent % reportIntervalPackets) == 0) )
                    {
                        sw.Stop();

                        double avgTripMs = sw.ElapsedMilliseconds / (double)packetsSent;
                        Logit(
                              $"OK seq={reply.SequenceNumber} "
                            + $"action={reply.Action} "
                            + $"trip avg={avgTripMs:0.00} ms "
                            + $"respLen={(reply.ReplyString?.Length ?? 0)}");
                        // TODO: parse/use reply.ReplyString if desired
                        packetsSent = 0;
                        sw = Stopwatch.StartNew();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (TimeoutException tex)
                {
                    // Expected during debugging pauses; the dispatcher resets the REQ socket for us.
                    Logit($"Timeout: {tex.Message}");
                    await Task.Delay(backoffMs, cts.Token);
                }
                catch (Exception ex)
                {
                    Logit($"Error: {ex.GetType().Name}: {ex.Message}");
                    await Task.Delay(backoffMs, cts.Token);
                }

                //try { await Task.Delay(pollIntervalMs, cts.Token); } catch { break; }
            }

            Logit("Client exiting.");
            return 0;
        }

        private static void Logit(string message)
        {
            Logger.LogIt(message);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
}
