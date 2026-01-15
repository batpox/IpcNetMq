IPC Communications using NetMQ (ZeroMQ)

Version: 26.1.14 - Tightened up some async handling. Put in error check to check for Server mode.
   Test: Turnaround time for a REQ-REP call is consistently 120 microseconds (0.12 ms) on machine (11th Gen Intel(R) Core(TM) i9-11900K @ 3.50GHz (3.40 GHz)

A concrete implementation of using ZeroMQ REQ-REP package/pattern. The advantantage over mixed language 'call' is separate processes, is a quick implementation,
easy to debug, version protection, cross framework, cross-platform, cross-language. Advantage over other IPC is simplicity and speed (90-150 microseconds per roundtrip call).

The Client makes a Remote Procedure Call (RPC) by sending an IpcPacket to a Server. Within the packet is the name
of the Procedure (Action) and the arguments (a list of name/value pairs in the Request string) and also what is expected
in the Reply (another list of name/value pairs).

The Server processes the procedure/method logic (named Action) according to those Request arguments 
and fills in the Reply.

Both the Request and Reply strings are serialized json, and any values are strings. Json and string are common denominator
that can be processed by most modern languages and byte-order issues are avoided.

Everything revolves around the information packet called IpcPacket that is constructed to handle most cases that I have encountered in my career.

The header holds things like a schema-version, timestamp, action name, and sequence number to help with communication errors.
Again, the Request string is a json-serialized list of name/value pairs (the 'in' arguments),
and a Reply string (also json-serialized list of name/value pairs).

It is assumed that type information for values is implicitly known by the Server/Clients.

Examples are given for C# and Python, with near-term plans for C++ and Java.

Benchmarks indicate approximately 90-120 microseconds for a REQ-REP roundtrip 'call'. For reference, an actual in-process call between Python and C# would take about 10-20 microseconds,
so, about 11x slower, but with the advantage of separate processes, separate languages, separate runtimes, and no need for wrappers or interop layers.

Use cases: The REQ-REP pattern in general - and ZeroMQ/NetMQ specifically - along with generic strings and accepted 
serialization makes for a good combination for inter-process (indeed, even inter-machine) calls.

Anyone who has tried to call python from C# knows the difficult issues involved. IpcNetMq provides a simple way around this.
For example, a C# program may need results from a python library (which is a vast landscape). You can use the included
python IpcNetMq server to allow this call, and not need to worry about bit-size, byte-order, language versions, etc. or building
wrappers around the calls.

The downside is that you will sacrifice about 11x speed, but less than 0.1 millisecond turnaround should be 
sufficient for many applications (e.g. I am using it for communications with my rower and also for Stride 3D communications)

Test Examples are included but there is a quick display of the salient parts of the client and server in the C# example.

**** Client
            using var client = new IpcClientNetMq(clientName, serverAddress) { LoggingLevel = 1 };
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

**** Server

                var server = new IpcServerNetMq("TestServer", ipcAddress);
                server.RunIpcServerLoop(UserActions.HandleAction);

   where HandleAction might be something like:
           public static IpcPacket HandleAction(IpcPacket inPacket)
           {
            switch (inPacket.Action)
            {
                case "do_get1":
                    return UserActions.do_get1(inPacket);
                case "do_get2":
                    return UserActions.do_get2(inPacket);
                case "TestEnter":
                    return UserActions.TestEnter(inPacket);
                case "TestExit":
                    return UserActions.TestExit(inPacket);

                default:


At any rate, enjoy and I hope it helps.

Cheers,
Daniel




