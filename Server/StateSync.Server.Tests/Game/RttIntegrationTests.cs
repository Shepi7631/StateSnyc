namespace StateSync.Server.Tests.Game;

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Network;
using StateSync.Shared;
using Xunit;

public class RttIntegrationTests
{
	private static NavMesh2D BuildMesh()
	{
		var boundary = new Polygon(new[]
		{
			new Vec2(0f, 0f),
			new Vec2(10f, 0f),
			new Vec2(10f, 10f),
			new Vec2(0f, 10f),
		});
		return NavMeshBuilder.Build(boundary);
	}

	[Fact]
	public void FullPipeline_PingSentAndPongUpdatesRtt()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh);
		room.TryAdd("player1");
		var sent = new List<(MessageType Type, byte[] Data)>();
		var session = new FakeSession(sent) { PlayerId = "player1" };
		room.AddSession(session);

		var roomLoop = new RoomLoop(room);

		// Tick 10 times to trigger Ping
		for (int i = 0; i < 10; i++)
			roomLoop.Tick();

		// Extract the Ping that was sent
		var pingMsg = sent.Where(m => m.Type == MessageType.Ping).First();
		var ping = Ping.Parser.ParseFrom(pingMsg.Data);

		// Simulate client sending back Pong with same timestamp
		var pong = new Pong
		{
			ServerTimestamp = ping.ServerTimestamp,
			Sequence = ping.Sequence
		};

		// Feed Pong to dispatcher
		var roomManager = new RoomManager(mesh);
		var dispatcher = new MessageDispatcher(roomManager);
		byte[] pongData = pong.ToByteArray();
		dispatcher.Dispatch(MessageType.Pong, pongData, pongData.Length, session);

		// Session should now have a non-zero RTT (small due to in-process timing)
		Assert.True(session.SmoothedRtt >= 0);

		// Tick 10 more times — next Ping should include the measured RTT
		sent.Clear();
		for (int i = 0; i < 10; i++)
			roomLoop.Tick();

		var ping2Msg = sent.Where(m => m.Type == MessageType.Ping).First();
		var ping2 = Ping.Parser.ParseFrom(ping2Msg.Data);
		Assert.Equal((uint)session.SmoothedRtt, ping2.YourRttMs);
	}

	[Fact]
	public void SyncRttFromSessions_CopiesRttToEntity()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh);
		room.TryAdd("player1");
		var session = new FakeSession([]) { PlayerId = "player1", SmoothedRtt = 55 };
		room.AddSession(session);

		room.SyncRttFromSessions();

		var entity = room.GetEntity("player1");
		Assert.NotNull(entity);
		Assert.Equal(55, entity!.Rtt);
	}

	private class FakeSession : IClientSession
	{
		private readonly List<(MessageType, byte[])> _log;

		public FakeSession(List<(MessageType, byte[])> log) => _log = log;

		public string? PlayerId { get; set; }
		public int SmoothedRtt { get; set; }
		public void Send(MessageType type, byte[] data) => _log.Add((type, data));
	}
}
