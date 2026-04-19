namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using StateSync.Shared;

public class RoomManagerTests
{
    private readonly RoomManager _manager = new();

    public RoomManagerTests()
    {
        _manager.CreateRoom("room1", maxPlayers: 2);
    }

    [Fact]
    public void HandleJoinRoom_Success_ReturnsAssignedPlayerIdAndAllPlayers()
    {
        var (error, _, response) = _manager.HandleJoinRoom("room1");

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
        Assert.NotEmpty(response.PlayerId);
        Assert.Single(response.PlayerIds);
    }

    [Fact]
    public void HandleJoinRoom_RoomNotFound_ReturnsErrorNoParams()
    {
        var (error, errorParams, response) = _manager.HandleJoinRoom("missing");

        Assert.Equal(ErrorCode.RoomNotFound, error);
        Assert.Empty(errorParams);
        Assert.Null(response);
    }

    [Fact]
    public void HandleJoinRoom_RoomFull_ReturnsErrorWithMaxPlayersParam()
    {
        _manager.HandleJoinRoom("room1");
        _manager.HandleJoinRoom("room1");

        var (error, errorParams, response) = _manager.HandleJoinRoom("room1");

        Assert.Equal(ErrorCode.RoomFull, error);
        Assert.Equal(2, errorParams[0]);
        Assert.Null(response);
    }

    [Fact]
    public void HandleJoinRoom_SecondPlayer_PlayerIdsContainsBothPlayers()
    {
        _manager.HandleJoinRoom("room1");
        var (_, _, response) = _manager.HandleJoinRoom("room1");

        Assert.Equal(2, response!.PlayerIds.Count);
    }

    [Fact]
    public void HandleCreateRoom_ValidMaxPlayers_ReturnsSuccessWithSixDigitRoomId()
    {
        var (error, _, response) = _manager.HandleCreateRoom(8);

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
        Assert.Equal(6, response.RoomId.Length);
        Assert.True(response.RoomId.All(char.IsDigit));
    }

    [Fact]
    public void HandleCreateRoom_MaxPlayersZero_ReturnsInvalidMaxPlayers()
    {
        var (error, errorParams, response) = _manager.HandleCreateRoom(0);

        Assert.Equal(ErrorCode.InvalidMaxPlayers, error);
        Assert.Empty(errorParams);
        Assert.Null(response);
    }

    [Fact]
    public void HandleCreateRoom_MaxPlayersNegative_ReturnsInvalidMaxPlayers()
    {
        var (error, _, response) = _manager.HandleCreateRoom(-1);

        Assert.Equal(ErrorCode.InvalidMaxPlayers, error);
        Assert.Null(response);
    }

    [Fact]
    public void HandleCreateRoom_MaxPlayersExceedsLimit_ReturnsInvalidMaxPlayers()
    {
        var (error, _, response) = _manager.HandleCreateRoom(17);

        Assert.Equal(ErrorCode.InvalidMaxPlayers, error);
        Assert.Null(response);
    }

    [Fact]
    public void HandleCreateRoom_BoundaryMin_ReturnsSuccess()
    {
        var (error, _, response) = _manager.HandleCreateRoom(1);

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
    }

    [Fact]
    public void HandleCreateRoom_BoundaryMax_ReturnsSuccess()
    {
        var (error, _, response) = _manager.HandleCreateRoom(16);

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
    }

    [Fact]
    public void HandleCreateRoom_CalledTwice_ReturnsDifferentRoomIds()
    {
        var (_, _, response1) = _manager.HandleCreateRoom(8);
        var (_, _, response2) = _manager.HandleCreateRoom(8);

        Assert.NotEqual(response1!.RoomId, response2!.RoomId);
    }
}
