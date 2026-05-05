namespace StateSync.Server.Tests.Game;

using System.Collections.Generic;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using Google.Protobuf;
using StateSync.Server.Game;
using StateSync.Shared;
using Xunit;

public class RoomLoopTests
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
	public void Tick_WithMoveInput_BroadcastsMoveEventAndPositionSync()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh);
		room.TryAdd("player1");
		var sent = new List<(MessageType Type, byte[] Data)>();
		var fakeSession = new FakeSession(sent);
		room.AddSession(fakeSession);

		room.EnqueueMoveInput("player1", new Vec2(5f, 5f));
		var roomLoop = new RoomLoop(room);
		roomLoop.Tick();

		// Should have at least 2 messages: MoveEvent + PositionSync
		Assert.True(sent.Count >= 2);

		// First message should be MoveEvent
		Assert.Equal(MessageType.MoveEvent, sent[0].Type);
		var moveEvent = MoveEvent.Parser.ParseFrom(sent[0].Data);
		Assert.Equal("player1", moveEvent.PlayerId);
		Assert.Equal(5f, moveEvent.TargetX, precision: 3);
		Assert.Equal(5f, moveEvent.TargetY, precision: 3);

		// Second message should be PositionSync
		Assert.Equal(MessageType.PositionSync, sent[1].Type);
		var posSync = PositionSync.Parser.ParseFrom(sent[1].Data);
		Assert.Equal("player1", posSync.PlayerId);
		Assert.True(posSync.IsMoving);
	}

	[Fact]
	public void MultipleTicks_EntityReachesDestination_StopsBroadcasting()
	{
		var mesh = BuildMesh();
		var room = new Room("test", 16, mesh, defaultSpeed: 100f);
		room.TryAdd("player1");
		var sent = new List<(MessageType Type, byte[] Data)>();
		var fakeSession = new FakeSession(sent);
		room.AddSession(fakeSession);

		// Target (1,1) from origin (0,0) has distance ~1.414.
		// With speed=100 and dt=0.1 (one tick), entity travels 10 units — far exceeds the distance.
		room.EnqueueMoveInput("player1", new Vec2(1f, 1f));
		var roomLoop = new RoomLoop(room);
		roomLoop.Tick(); // entity starts moving and arrives in this tick

		sent.Clear();
		roomLoop.Tick(); // no more moving entities

		// No PositionSync if no moving entities
		Assert.DoesNotContain(sent, m => m.Type == MessageType.PositionSync);
	}

	private class FakeSession : IClientSession
	{
		private readonly List<(MessageType, byte[])> _log;

		public FakeSession(List<(MessageType, byte[])> log) => _log = log;

		public string? PlayerId { get; } = null;
		public int SmoothedRtt { get; set; }

		public void Send(MessageType type, byte[] data) => _log.Add((type, data));
	}
}
