using StateSync.Server.Game;
using StateSync.Server.Network;

var roomManager = new RoomManager();
roomManager.CreateRoom("room1");

var dispatcher = new MessageDispatcher(roomManager);
var server = new TcpServer(7777, dispatcher);

await server.StartAsync();
