namespace StateSync.Server.Network;

using Google.Protobuf;
using StateSync.Server.Game;
using StateSync.Shared;

public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    public MessageDispatcher(RoomManager roomManager) => _roomManager = roomManager;

    public byte[] Dispatch(MessageType type, byte[] data) => type switch
    {
        MessageType.JoinRoom   => HandleJoinRoom(data),
        MessageType.CreateRoom => HandleCreateRoom(data),
        _ => PacketWriter.WriteServerPacket(type, ErrorCode.InvalidInput, [], [])
    };

    private byte[] HandleJoinRoom(byte[] data)
    {
        JoinRoom request;
        try { request = JoinRoom.Parser.ParseFrom(data); }
        catch (InvalidProtocolBufferException)
        {
            return PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleJoinRoom(request.RoomId);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.JoinRoom, error, errorParams, responseData);
    }

    private byte[] HandleCreateRoom(byte[] data)
    {
        CreateRoom request;
        try { request = CreateRoom.Parser.ParseFrom(data); }
        catch (InvalidProtocolBufferException)
        {
            return PacketWriter.WriteServerPacket(MessageType.CreateRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleCreateRoom(request.MaxPlayers);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.CreateRoom, error, errorParams, responseData);
    }
}
