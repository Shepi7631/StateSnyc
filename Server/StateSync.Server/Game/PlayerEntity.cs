namespace StateSync.Server.Game;

using System.Collections.Generic;
using Pathfinding.Algorithm;
using Pathfinding.Data;

public class PlayerEntity
{
	public string PlayerId { get; }
	public Vec2 Position { get; private set; }
	public float Speed { get; }
	public bool IsMoving { get; private set; }
	public Vec2 Target { get; private set; }

	private readonly NavMesh2D _Mesh;
	private Vec2[] _Path = [];
	private int _PathIndex;

	public IReadOnlyList<Vec2> Path => _Path;
	public int CurrentPathIndex => _PathIndex;
	public int Rtt { get; set; }

	public PlayerEntity(string playerId, Vec2 position, float speed, NavMesh2D mesh)
	{
		PlayerId = playerId;
		Position = position;
		Speed = speed;
		_Mesh = mesh;
	}

	public bool SetDestination(Vec2 target)
	{
		IReadOnlyList<int>? corridor = AStarSolver.Solve(_Mesh, Position, target);
		if (corridor == null)
			return false;

		IReadOnlyList<Vec2> waypoints = FunnelSolver.Solve(_Mesh, corridor, Position, target);
		_Path = ToArray(waypoints);
		_PathIndex = 0;
		Target = target;
		IsMoving = true;
		return true;
	}

	public void Tick(float deltaTime)
	{
		if (!IsMoving)
			return;

		float remaining = Speed * deltaTime;
		while (remaining > 0f && _PathIndex < _Path.Length - 1)
		{
			Vec2 next = _Path[_PathIndex + 1];
			float dist = Position.DistanceTo(next);

			if (remaining >= dist)
			{
				Position = next;
				_PathIndex++;
				remaining -= dist;
			}
			else
			{
				Vec2 direction = (next - Position).Normalized();
				Position = Position + direction * remaining;
				remaining = 0f;
			}
		}

		if (_PathIndex >= _Path.Length - 1)
			IsMoving = false;
	}

	private static Vec2[] ToArray(IReadOnlyList<Vec2> list)
	{
		Vec2[] arr = new Vec2[list.Count];
		for (int i = 0; i < list.Count; i++)
			arr[i] = list[i];
		return arr;
	}
}
