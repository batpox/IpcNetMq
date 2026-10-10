using IpcNetMq.IpcNetMqHelpers;
using NetMQ;
using NetMQ.Sockets;
using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace IpcNetMq.Tests;

public class TransportTests
{
    private static string Address() => "inproc://ipc-test-" + Guid.NewGuid().ToString("N");
    private static IpcPacket Request() => new() { Action = "echo", ClientId = "test-client" };
    private static void Until(Func<bool> test)
    {
        var clock = Stopwatch.StartNew();
        while (!test())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Test operation did not complete.");
            Thread.Sleep(5);
        }
    }
    private static Task Worker(Action action) => Task.Factory.StartNew(action,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    [Fact]
    public async Task DisposingOneServerDoesNotTerminateOtherConnections()
    {
        var address = Address();
        using var active = new IpcServerNetMq("active", address);
        using var client = new IpcClientNetMq("client", address);
        var lifetime = active.RunIpcServerLoopOnBackgroundThreadAsync(_ => new IpcPacket());
        Assert.Equal(2, (await client.CallIpcMethodAsync(Request()).WaitAsync(TimeSpan.FromSeconds(5))).SequenceNumber);
        using (var other = new IpcServerNetMq("other", Address())) other.EnsurePollingReady();
        var reply = await client.CallIpcMethodAsync(Request()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, reply.SequenceNumber);
        Assert.Equal("test-client", reply.ClientId);
        active.Dispose();
        await lifetime.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CancellationAbandonsOldExchangeAndNextCallDoesNotWaitForItsTimeout()
    {
        var address = Address();
        using var received = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var server = new IpcServerNetMq("server", address);
        var calls = 0;
        var lifetime = server.RunIpcServerLoopOnBackgroundThreadAsync(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1) { received.Set(); release.Wait(TimeSpan.FromSeconds(5)); }
            return new IpcPacket();
        });
        using var client = new IpcClientNetMq("client", address);
        using var cancel = new CancellationTokenSource();
        var first = client.CallIpcMethodAsync(Request(), receiveTimeout: TimeSpan.FromSeconds(20), ct: cancel.Token);
        Assert.True(received.Wait(TimeSpan.FromSeconds(5)));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.Set();
        var reply = await client.CallIpcMethodAsync(Request()).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(4, reply.SequenceNumber);
        server.Dispose();
        await lifetime.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ClientDisposalCompletesActiveAndQueuedCallsAndRejectsNewCalls()
    {
        var address = Address();
        using var received = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var server = new IpcServerNetMq("server", address);
        var lifetime = server.RunIpcServerLoopOnBackgroundThreadAsync(_ =>
        {
            received.Set(); release.Wait(TimeSpan.FromSeconds(5)); return new IpcPacket();
        });
        using var client = new IpcClientNetMq("client", address);
        var first = client.CallIpcMethodAsync(Request(), receiveTimeout: TimeSpan.FromSeconds(30));
        Assert.True(received.Wait(TimeSpan.FromSeconds(5)));
        var queued = Enumerable.Range(0, 5).Select(_ => client.CallIpcMethodAsync(Request())).ToArray();
        var watch = Stopwatch.StartNew();
        client.Dispose();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first);
        foreach (var call in queued) await Assert.ThrowsAsync<ObjectDisposedException>(() => call);
        Assert.Throws<ObjectDisposedException>(() => { _ = client.CallIpcMethodAsync(Request()); });
        release.Set(); server.Dispose();
        await lifetime.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task IdleServerCancellationStopsAndAllowsRestart()
    {
        using var server = new IpcServerNetMq("server", Address());
        using var cancel = new CancellationTokenSource();
        var lifetime = server.RunIpcServerLoopOnBackgroundThreadAsync(_ => new IpcPacket(), cancel.Token);
        Assert.Throws<InvalidOperationException>(() => server.TryGetRequest(out _));
        cancel.Cancel();
        await lifetime.WaitAsync(TimeSpan.FromSeconds(2));
        using var restartCancel = new CancellationTokenSource();
        var restarted = server.RunIpcServerLoopOnBackgroundThreadAsync(_ => new IpcPacket(), restartCancel.Token);
        restartCancel.Cancel();
        await restarted.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"sequence_number\":2147483647}")]
    [InlineData("multipart")]
    public async Task PollingServerRecoversAfterInvalidMessagesAndEchoesCorrelation(string invalid)
    {
        await Worker(() =>
        {
            var address = Address();
            using var server = new IpcServerNetMq("server", address);
            server.EnsurePollingReady();
            using var raw = new RequestSocket();
            raw.Options.Linger = TimeSpan.Zero;
            raw.Connect(address);
            if (invalid == "multipart") raw.SendMoreFrame("{}").SendFrame("{}");
            else raw.SendFrame(invalid);
            string? error = null;
            Until(() => { Assert.False(server.TryGetRequest(out _)); return raw.TryReceiveFrameString(TimeSpan.Zero, out error); });
            Assert.StartsWith("error:", JsonHelpers.DeserializeFromJsonString(error!).Status);
            raw.SendFrame("{\"sequence_number\":7,\"client_id\":\"poll-client\",\"action\":\"echo\"}");
            IpcPacket request = null!;
            Until(() => server.TryGetRequest(out request));
            Assert.Equal(7, request.SequenceNumber);
            server.SendReply(new IpcPacket());
            Assert.True(raw.TryReceiveFrameString(TimeSpan.FromSeconds(2), out var json));
            var reply = JsonHelpers.DeserializeFromJsonString(json);
            Assert.Equal(8, reply.SequenceNumber);
            Assert.Equal("poll-client", reply.ClientId);
        }).WaitAsync(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task HandlerErrorsReturnCorrelatedErrorAndServerKeepsServing()
    {
        var address = Address();
        using var server = new IpcServerNetMq("server", address);
        var count = 0;
        var lifetime = server.RunIpcServerLoopOnBackgroundThreadAsync(_ =>
            Interlocked.Increment(ref count) == 1 ? throw new InvalidOperationException("test failure") : new IpcPacket());
        using var client = new IpcClientNetMq("client", address);
        var error = await client.CallIpcMethodAsync(Request()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("test failure", error.Status);
        Assert.Equal("test-client", error.ClientId);
        Assert.Equal("", (await client.CallIpcMethodAsync(Request())).Status);
        server.Dispose(); await lifetime;
    }

    [Fact]
    public async Task ConcurrentCallsAreSerializedAndCorrelated()
    {
        var address = Address();
        using var server = new IpcServerNetMq("server", address);
        var lifetime = server.RunIpcServerLoopOnBackgroundThreadAsync(request => new IpcPacket { ReplyString = request.RequestString });
        using var client = new IpcClientNetMq("client", address);
        var replies = await Task.WhenAll(Enumerable.Range(0, 40).Select(async i =>
        {
            var reply = await client.CallIpcMethodAsync(new IpcPacket { Action = "echo", RequestString = i.ToString() });
            Assert.Equal(i.ToString(), reply.ReplyString);
            return reply;
        })).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(40, replies.Select(r => r.SequenceNumber).Distinct().Count());
        server.Dispose(); await lifetime;
    }

    [Fact]
    public async Task LegacyAndPollingApisRejectWrongThreadAndModeMixing()
    {
        await Worker(() =>
        {
            var address = Address();
            using var bound = new IpcServerNetMq("bound", address);
            bound.EnsurePollingReady();
            using var client = new IpcClientNetMq("legacy", address);
            Assert.True(client.OpenConnection(out _));
            Assert.Throws<InvalidOperationException>(() => { _ = client.CallIpcMethodAsync(Request()); });
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => client.CloseConnection(out _))).GetAwaiter().GetResult();
            Assert.True(client.CloseConnection(out _));
            using var server = new IpcServerNetMq("poll", Address());
            server.EnsurePollingReady();
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => server.TryGetRequest(out _))).GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => { _ = server.RunIpcServerLoopOnBackgroundThreadAsync(_ => new IpcPacket()); });
        });
    }

    [Fact]
    public async Task TcpTimeoutAndInvalidReplyResetSocketWithoutReplayingRequests()
    {
        await Worker(() =>
        {
            using var raw = new ResponseSocket();
            raw.Options.Linger = TimeSpan.Zero;
            var port = raw.BindRandomPort("tcp://127.0.0.1");
            using var client = new IpcClientNetMq("tcp", "tcp://127.0.0.1:" + port);
            var first = client.CallIpcMethodAsync(Request(), receiveTimeout: TimeSpan.FromMilliseconds(500));
            Assert.True(raw.TryReceiveFrameString(TimeSpan.FromSeconds(3), out var requestJson));
            Until(() => first.IsCompleted);
            Assert.Throws<TimeoutException>(() => first.GetAwaiter().GetResult());
            // An abandoned REP exchange may have no connected peer; reopen the test server.
            raw.Dispose();
            using var recovered = new ResponseSocket();
            recovered.Options.Linger = TimeSpan.Zero;
            var recoveredPort = recovered.BindRandomPort("tcp://127.0.0.1");
            client.ServerAddress = "tcp://127.0.0.1:" + recoveredPort;
            var second = client.CallIpcMethodAsync(Request());
            Assert.True(recovered.TryReceiveFrameString(TimeSpan.FromSeconds(3), out requestJson));
            Assert.Equal(3, JsonHelpers.DeserializeFromJsonString(requestJson).SequenceNumber);
            recovered.SendMoreFrame("{\"sequence_number\":4}").SendFrame("unsupported");
            Until(() => second.IsCompleted);
            Assert.Throws<FormatException>(() => second.GetAwaiter().GetResult());
            var third = client.CallIpcMethodAsync(Request());
            Assert.True(recovered.TryReceiveFrameString(TimeSpan.FromSeconds(3), out requestJson));
            Assert.Equal(5, JsonHelpers.DeserializeFromJsonString(requestJson).SequenceNumber);
            recovered.SendFrame("{\"sequence_number\":6}");
            Assert.Equal(6, third.GetAwaiter().GetResult().SequenceNumber);
        }).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(15000)]
    public async Task TinyReceiveTimeoutsExpireInsteadOfBecomingInfinite(long ticks)
    {
        await Worker(() =>
        {
            var address = Address();
            using var raw = new ResponseSocket();
            raw.Options.Linger = TimeSpan.Zero;
            raw.Bind(address);
            using var client = new IpcClientNetMq("tiny-timeout", address);
            var call = client.CallIpcMethodAsync(Request(), receiveTimeout: TimeSpan.FromTicks(ticks));
            Until(() => call.IsCompleted);
            Assert.Throws<TimeoutException>(() => call.GetAwaiter().GetResult());
        }).WaitAsync(TimeSpan.FromSeconds(8));
    }
}
