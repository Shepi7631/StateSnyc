namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using StateSync.Shared;

public class GameLoopTests
{
	[Fact]
	public void Tick_IncrementsTick()
	{
		var loop = new GameLoop(tickRate: 10);

		Assert.Equal(0u, loop.CurrentTick);
		loop.Tick();
		Assert.Equal(1u, loop.CurrentTick);
	}

	[Fact]
	public void DeltaTime_Is_InverseOfTickRate()
	{
		var loop = new GameLoop(tickRate: 10);

		Assert.Equal(0.1f, loop.DeltaTime, precision: 5);
	}

	[Fact]
	public void EnqueueBroadcast_And_Flush_ClearsQueue()
	{
		var loop = new GameLoop(tickRate: 10);
		var flushed = new List<(MessageType Type, byte[] Data)>();
		var session = new FakeSession(flushed);

		loop.EnqueueBroadcast([session], MessageType.MoveEvent, [1, 2, 3]);
		loop.Flush();

		Assert.Single(flushed);
		Assert.Equal(MessageType.MoveEvent, flushed[0].Type);
		Assert.Equal(new byte[] { 1, 2, 3 }, flushed[0].Data);
	}

	[Fact]
	public void Flush_EmptyQueue_DoesNothing()
	{
		var loop = new GameLoop(tickRate: 10);
		loop.Flush(); // no exception
	}

	[Fact]
	public void EnqueueSend_SingleSession()
	{
		var loop = new GameLoop(tickRate: 10);
		var flushed = new List<(MessageType Type, byte[] Data)>();
		var session = new FakeSession(flushed);

		loop.EnqueueSend(session, MessageType.PositionSync, [4, 5]);
		loop.Flush();

		Assert.Single(flushed);
		Assert.Equal(MessageType.PositionSync, flushed[0].Type);
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
