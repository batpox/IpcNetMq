using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{
    public abstract class IpcServerBaseNetMq : IDisposable
    {
        private bool _disposed;
        protected static readonly Mutex ReqRepMutex = new Mutex();

        protected readonly string ServerAddress;
        protected ResponseSocket ServerSocket;
        protected bool IsBound;

        protected readonly NetMQRuntime Runtime;

        protected IpcServerBaseNetMq(string serverAddress)
        {
            ServerAddress = serverAddress;
            Runtime = new NetMQRuntime();
        }

        protected virtual void Logit(string msg)
        {
            Console.WriteLine(msg);
        }

        protected virtual bool OpenSocket(out string reason)
        {
            reason = null;

            try
            {
                if (ServerSocket != null)
                {
                    ServerSocket.Dispose();
                    ServerSocket = null;
                }

                ServerSocket = new ResponseSocket();
                ServerSocket.Bind(ServerAddress);
                IsBound = true;

                return true;
            }
            catch (Exception ex)
            {
                reason = "OpenSocket failed: " + ex.Message;
                return false;
            }
        }

        protected virtual bool CloseSocket(out string reason)
        {
            reason = null;

            try
            {
                if (IsBound && ServerSocket != null)
                {
                    ServerSocket.Unbind(ServerAddress);
                    ServerSocket.Dispose();
                    ServerSocket = null;
                    IsBound = false;
                }

                NetMQConfig.Cleanup();
                return true;
            }
            catch (Exception ex)
            {
                reason = "CloseSocket failed: " + ex.Message;
                return false;
            }
        }

        protected Task RunServerLoopAsync(
            Func<Task<IpcPacket>> receiveFunc,
            Func<IpcPacket, Task> respondFunc,
            string label)
        {
            return Task.Run(async () =>
            {
                string reason = null;
                int retries = 0;
                const int maxRetries = 10;

                while (retries < maxRetries)
                {
                    try
                    {
                        retries++;

                        if (OpenSocket(out reason))
                        {
                            retries = 0;
                            Logit("[" + label + "] Bound to " + ServerAddress + ". Listening...");

                            bool keepRunning = true;

                            while (keepRunning)
                            {
                                try
                                {
                                    ReqRepMutex.WaitOne();

                                    var packet = await receiveFunc().ConfigureAwait(false);
                                    if (packet != null)
                                    {
                                        await respondFunc(packet).ConfigureAwait(false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logit("[" + label + "] Read error: " + ex.Message);
                                    keepRunning = false;
                                    CloseSocket(out reason);
                                }
                                finally
                                {
                                    ReqRepMutex.ReleaseMutex();
                                }
                            }
                        }
                        else
                        {
                            Logit("[" + label + "] Retry failed: " + reason + ". Attempt " + retries);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logit("[" + label + "] Fatal: " + ex.Message);
                    }

                    CloseSocket(out reason);
                    Logit("[" + label + "] Disconnected. Retrying...");
                }

                Logit("[" + label + "] Exiting after " + maxRetries + " retries.");
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (ServerSocket != null)
                    {
                        ServerSocket.Dispose();
                        ServerSocket = null;
                    }

                    if (Runtime != null)
                    {
                        Runtime.Dispose();
                    }

                    NetMQConfig.Cleanup();
                }

                _disposed = true;
            }
        }
    }
}
