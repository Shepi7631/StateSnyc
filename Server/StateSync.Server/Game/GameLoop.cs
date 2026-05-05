namespace StateSync.Server.Game;

using System.Collections.Generic;
using StateSync.Shared;

public class GameLoop
{
	private readonly List<(IReadOnlyList<IClientSession> Sessions, MessageType Type, byte[] Data)> _outgoing = [];

	public uint CurrentTick { get; private set; }
	public float DeltaTime { get; }

	public GameLoop(int tickRate)
	{
		DeltaTime = 1f / tickRate;
	}

	public void Tick()
	{
		CurrentTick++;
	}

	public void EnqueueBroadcast(IReadOnlyList<IClientSession> sessions, MessageType type, byte[] data)
	{
		_outgoing.Add((sessions, type, data));
	}

	public void EnqueueSend(IClientSession session, MessageType type, byte[] data)
	{
		_outgoing.Add(([session], type, data));
	}

	public void Flush()
	{
		foreach (var (sessions, type, data) in _outgoing)
		{
			foreach (var session in sessions)
				session.Send(type, data);
		}
		_outgoing.Clear();
	}
}
