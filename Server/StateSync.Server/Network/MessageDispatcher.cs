namespace StateSync.Server.Network;

using Google.Protobuf;
using StateSync.Server.Game;
using StateSync.Shared;

public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    public MessageDispatcher(RoomManager roomManager) => _roomManager = roomManager;

    public (byte[] Buffer, int Length) Dispatch(MessageType type, byte[] data, int dataLength) => type switch
    {
        MessageType.JoinRoom   => HandleJoinRoom(data, dataLength),
        MessageType.CreateRoom => HandleCreateRoom(data, dataLength),
        _ => PacketWriter.WriteServerPacket(type, ErrorCode.InvalidInput, [], [])
    };

    private (byte[] Buffer, int Length) HandleJoinRoom(byte[] data, int dataLength)
    {
        JoinRoom request;
        // AsSpan(0, dataLength)：ArrayPool.Rent 可能返回比请求更大的缓冲，必须限定有效范围
        try { request = JoinRoom.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleJoinRoom(request.RoomId);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.JoinRoom, error, errorParams, responseData);
    }

    private (byte[] Buffer, int Length) HandleCreateRoom(byte[] data, int dataLength)
    {
        CreateRoom request;
        // AsSpan(0, dataLength)：ArrayPool.Rent 可能返回比请求更大的缓冲，必须限定有效范围
        try { request = CreateRoom.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return PacketWriter.WriteServerPacket(MessageType.CreateRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleCreateRoom(request.MaxPlayers);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.CreateRoom, error, errorParams, responseData);
    }
}
