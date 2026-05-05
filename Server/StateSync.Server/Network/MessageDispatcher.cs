namespace StateSync.Server.Network;

using System.Buffers;
using Google.Protobuf;
using StateSync.Server.Game;
using StateSync.Shared;
using Pathfinding.Data;

public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    public MessageDispatcher(RoomManager roomManager) => _roomManager = roomManager;

    public (byte[]? Buffer, int Length) Dispatch(MessageType type, byte[] data, int dataLength, IClientSession session) => type switch
    {
        MessageType.JoinRoom => HandleJoinRoom(data, dataLength, (ClientSession)session),
        MessageType.CreateRoom => HandleCreateRoom(data, dataLength),
        MessageType.MoveRequest => HandleMoveRequest(data, dataLength, (ClientSession)session),
        MessageType.Pong => HandlePong(data, dataLength, session),
        _ => WriteResponse(type, ErrorCode.InvalidInput, [], [])
    };

    private (byte[] Buffer, int Length) HandleJoinRoom(byte[] data, int dataLength, ClientSession session)
    {
        JoinRoom request;
        try { request = JoinRoom.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return WriteResponse(MessageType.JoinRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleJoinRoom(request.RoomId);
        if (error == ErrorCode.Success && response != null)
        {
            session.PlayerId = response.PlayerId;
            var room = _roomManager.GetRoom(request.RoomId);
            if (room != null)
            {
                session.Room = room;
                room.AddSession(session);
            }
        }
        byte[] responseData = response?.ToByteArray() ?? [];
        return WriteResponse(MessageType.JoinRoom, error, errorParams, responseData);
    }

    private (byte[] Buffer, int Length) HandleCreateRoom(byte[] data, int dataLength)
    {
        CreateRoom request;
        try { request = CreateRoom.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return WriteResponse(MessageType.CreateRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleCreateRoom(request.MaxPlayers);
        byte[] responseData = response?.ToByteArray() ?? [];
        return WriteResponse(MessageType.CreateRoom, error, errorParams, responseData);
    }

    private (byte[]? Buffer, int Length) HandleMoveRequest(byte[] data, int dataLength, ClientSession session)
    {
        if (session.PlayerId == null || session.Room == null)
            return WriteResponse(MessageType.MoveRequest, ErrorCode.PlayerNotInRoom, [], []);

        MoveRequest request;
        try { request = MoveRequest.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return WriteResponse(MessageType.MoveRequest, ErrorCode.InvalidInput, [], []);
        }

        session.Room.EnqueueMoveInput(session.PlayerId, new Vec2(request.TargetX, request.TargetY));
        return (null, 0); // no immediate response
    }

    private static (byte[]? Buffer, int Length) HandlePong(byte[] data, int dataLength, IClientSession session)
    {
        Pong pong;
        try { pong = Pong.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return (null, 0);
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int rttSample = (int)(now - (long)pong.ServerTimestamp);
        if (rttSample < 0) rttSample = 0;

        session.SmoothedRtt = (int)(0.2 * rttSample + 0.8 * session.SmoothedRtt);
        return (null, 0);
    }

    private static (byte[] Buffer, int Length) WriteResponse(MessageType type, ErrorCode error, int[] errorParams, byte[] data) =>
        PacketWriter.WriteServerPacket(type, error, errorParams, data);
}
