IPC Communications using NetMQ (ZeroMQ)

A concrete implementation of using the ZeroMQ REQ-REP pattern. The advantantage is a quick implementation,
easy to debug, cross framework, cross-platform, cross-language.

The Client makes a Remote Procedure Call (RPC) by sending an IpcPacket to a Server. Within the packet is the name
of the Procedure (Action) and the arguments (a list of name/value pairs in the Request string) and also what is expected
in the Response (another list of name/value pairs).

The Server processes the procedure/method logic (named Action) according to those Request arguments 
and fills in the Response.

Both the Request and Response strings are serialized json, and any values are strings. Json and string are common denominator
that can be processed by any modern language.

Everything revolves around the information packet called IpcPacket that is constructed to handle most cases that I have encountered in my career.

The header holds things like a schema-version, timestamp, action name, and sequence number to help with communication errors.
Again, the Request string is a json-serialized list of name/value pairs (the 'in' arguments),
and a Response string (also json-serialized list of name/value pairs).

It is assumed that type information for values is implicitly known by the Server/Clients.

Examples are given for C# and Python, with near-term plans for C++ and Java.

Benchmarks indicate approximately 90 microseconds for a REQ-REP roundtrip 'call'.

Use cases: The REQ-REP pattern in general - and ZeroMQ specifically - along with generic strings and accepted 
serialization makes for a good combination for inter-process (indeed, even inter-machine) calls.

Anyone who has tried to call python from C# knows the difficult issues involved. IpcNetMq provides a simple way around this.
For example, a C# program may need results from a python library (which is a vast landscape). You can use the included
python IpcNetMq server to allow this call, and not need to worry about bit-size, language versions, etc. or building
wrappers around the calls.

The downside is that you will sacrifice speed, but the less than 0.1 millisecond turnaround should be 
sufficient for most applications.

Cheers,
Daniel




