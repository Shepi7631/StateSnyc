namespace StateSync.Server.Tests.Network;

using System;
using System.Collections.Generic;
using Google.Protobuf;
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Network;
using StateSync.Shared;
using Xunit;

public class PongHandlerTests
{
	private static RoomManager CreateRoomManager()
	{
		var vertices = new Vec2[] { new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f) };
		var triangles = new NavTriangle[]
		{
			new(0, 0, 1, 2, 1, -1, -1),
			new(1, 0, 2, 3, -1, -1, 0)
		};
		return new RoomManager(new NavMesh2D(vertices, triangles));
	}

	[Fact]
	public void HandlePong_UpdatesSmoothedRtt()
	{
		var dispatcher = new MessageDispatcher(CreateRoomManager());
		var session = new FakeClientSession { PlayerId = "p1" };

		long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var pong = new Pong { ServerTimestamp = (ulong)(now - 50), Sequence = 1 };
		byte[] data = pong.ToByteArray();

		dispatcher.Dispatch(MessageType.Pong, data, data.Length, session);

		// EMA: 0.2 * ~50 + 0.8 * 0 ≈ 10 (approximate due to timing)
		Assert.True(session.SmoothedRtt > 0);
		Assert.True(session.SmoothedRtt < 60);
	}

	[Fact]
	public void HandlePong_ReturnsNullResponse()
	{
		var dispatcher = new MessageDispatcher(CreateRoomManager());
		var session = new FakeClientSession { PlayerId = "p1" };

		long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var pong = new Pong { ServerTimestamp = (ulong)(now - 30), Sequence = 1 };
		byte[] data = pong.ToByteArray();

		var (buffer, length) = dispatcher.Dispatch(MessageType.Pong, data, data.Length, session);

		Assert.Null(buffer);
		Assert.Equal(0, length);
	}

	private class FakeClientSession : IClientSession
	{
		public string? PlayerId { get; set; }
		public int SmoothedRtt { get; set; }
		public List<(MessageType, byte[])> Sent { get; } = [];
		public void Send(MessageType type, byte[] data) => Sent.Add((type, data));
	}
}
