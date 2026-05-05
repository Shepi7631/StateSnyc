namespace StateSync.Server.Monitor;

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using StateSync.Server.Game;

public class MonitorServer
{
	private readonly WebApplication _app;

	public MonitorServer(RoomManager roomManager, int port = 8080)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls($"http://localhost:{port}");
		_app = builder.Build();

		_app.MapGet("/api/rooms", () =>
		{
			var rooms = roomManager.GetAllRooms();
			var result = rooms.Select(r => new
			{
				roomId = r.RoomId,
				playerCount = r.Players.Count
			});
			return Results.Json(result);
		});

		_app.MapGet("/api/rooms/{roomId}/state", (string roomId) =>
		{
			var room = roomManager.GetRoom(roomId);
			if (room == null)
				return Results.NotFound();

			var players = room.Players;
			var playerStates = new List<object>();
			foreach (var pid in players)
			{
				var entity = room.GetEntity(pid);
				if (entity == null) continue;
				playerStates.Add(new
				{
					playerId = entity.PlayerId,
					position = new { x = entity.Position.X, z = entity.Position.Z },
					target = new { x = entity.Target.X, z = entity.Target.Z },
					path = entity.Path.Select(p => new { x = p.X, z = p.Z }).ToArray(),
					speed = entity.Speed,
					isMoving = entity.IsMoving,
					rttMs = entity.Rtt
				});
			}

			return Results.Json(new
			{
				roomId = room.RoomId,
				tick = room.CurrentTick,
				players = playerStates
			});
		});

		_app.MapGet("/api/rooms/{roomId}/navmesh", (string roomId) =>
		{
			var room = roomManager.GetRoom(roomId);
			if (room == null)
				return Results.NotFound();

			var mesh = room.NavMesh;
			var vertices = mesh.Vertices.Select(v => new { x = v.X, z = v.Z }).ToArray();
			var triangles = mesh.Triangles.Select(t => new int[] { t.V0, t.V1, t.V2 }).ToArray();

			return Results.Json(new { vertices, triangles });
		});

		_app.MapGet("/", () => Results.Content(MonitorPage.Html, "text/html"));
	}

	public async Task StartAsync(CancellationToken ct)
	{
		await _app.StartAsync(ct);
		await _app.WaitForShutdownAsync(ct);
	}
}
