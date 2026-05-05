namespace StateSync.Server.Tests.Game;

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Shared;
using Xunit;

public class RoomLoopRttTests
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
	public void Tick10_BroadcastsPing()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh);
		room.TryAdd("player1");
		var sent = new List<(MessageType Type, byte[] Data)>();
		var session = new FakeSession(sent);
		room.AddSession(session);

		var roomLoop = new RoomLoop(room);

		// Tick 10 times — ping is sent on tick % 10 == 0 (i.e., tick 10)
		for (int i = 0; i < 10; i++)
			roomLoop.Tick();

		var pings = sent.Where(m => m.Type == MessageType.Ping).ToList();
		Assert.Single(pings);

		var ping = Ping.Parser.ParseFrom(pings[0].Data);
		Assert.True(ping.ServerTimestamp > 0);
		Assert.Equal(1u, ping.Sequence);
	}

	[Fact]
	public void Tick20_BroadcastsTwoPings_WithIncrementingSequence()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh);
		room.TryAdd("player1");
		var sent = new List<(MessageType Type, byte[] Data)>();
		var session = new FakeSession(sent);
		room.AddSession(session);

		var roomLoop = new RoomLoop(room);

		for (int i = 0; i < 20; i++)
			roomLoop.Tick();

		var pings = sent.Where(m => m.Type == MessageType.Ping).ToList();
		Assert.Equal(2, pings.Count);

		var ping1 = Ping.Parser.ParseFrom(pings[0].Data);
		var ping2 = Ping.Parser.ParseFrom(pings[1].Data);
		Assert.Equal(1u, ping1.Sequence);
		Assert.Equal(2u, ping2.Sequence);
	}

	[Fact]
	public void Ping_IncludesSessionSmoothedRtt()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh);
		room.TryAdd("player1");
		var sent = new List<(MessageType Type, byte[] Data)>();
		var session = new FakeSession(sent) { SmoothedRtt = 42 };
		room.AddSession(session);

		var roomLoop = new RoomLoop(room);

		for (int i = 0; i < 10; i++)
			roomLoop.Tick();

		var pings = sent.Where(m => m.Type == MessageType.Ping).ToList();
		var ping = Ping.Parser.ParseFrom(pings[0].Data);
		Assert.Equal(42u, ping.YourRttMs);
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
