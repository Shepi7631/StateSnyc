using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Monitor;
using StateSync.Server.Network;

var vertices = new Vec2[]
{
    new(0f, 0f), new(100f, 0f), new(100f, 100f), new(0f, 100f)
};
var triangles = new NavTriangle[]
{
    new(0, 0, 1, 2, 1, -1, -1),
    new(1, 0, 2, 3, -1, -1, 0)
};
var navMesh = new NavMesh2D(vertices, triangles);

var roomManager = new RoomManager(navMesh);
roomManager.CreateRoom("room1");

var room = roomManager.GetRoom("room1")!;
var roomLoop = new RoomLoop(room);

var dispatcher = new MessageDispatcher(roomManager);
var server = new TcpServer(7777, dispatcher);
var monitor = new MonitorServer(roomManager);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("GM Monitor: http://localhost:8080");

await Task.WhenAll(
    roomLoop.RunAsync(cts.Token),
    server.StartAsync(cts.Token),
    monitor.StartAsync(cts.Token)
);
