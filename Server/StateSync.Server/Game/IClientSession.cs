namespace StateSync.Server.Game;

using StateSync.Shared;

public interface IClientSession
{
	string? PlayerId { get; }
	int SmoothedRtt { get; set; }
	void Send(MessageType type, byte[] data);
}
