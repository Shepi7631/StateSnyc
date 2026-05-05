using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Shared;

namespace StateSync.Client.Movement
{
	public class RemotePlayerController
	{
		private readonly NavMesh2D _Mesh;
		private readonly RttTracker _Rtt;

		private Vec2[] _Path = System.Array.Empty<Vec2>();
		private int _PathIndex;

		public string PlayerId { get; }
		public Vec2 Position { get; private set; }
		public float Speed { get; private set; }
		public Vec2 Target { get; private set; }
		public bool IsMoving { get; private set; }

		public RemotePlayerController(string playerId, NavMesh2D mesh, Vec2 startPos, RttTracker rtt)
		{
			PlayerId = playerId;
			_Mesh = mesh;
			_Rtt = rtt;
			Position = startPos;
		}

		public void OnMoveEvent(MoveEvent evt)
		{
			Target = new Vec2(evt.TargetX, evt.TargetY);
			Speed = evt.Speed;

			var corridor = AStarSolver.Solve(_Mesh, Position, Target);
			if (corridor == null)
				return;

			var waypoints = FunnelSolver.Solve(_Mesh, corridor, Position, Target);
			_Path = ToArray(waypoints);
			_PathIndex = 0;
			IsMoving = true;
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

		public void OnPositionSync(PositionSync sync)
		{
			var serverPos = new Vec2(sync.PosX, sync.PosY);
			float distance = Position.DistanceTo(serverPos);

			float snapThreshold = _Rtt.SnapThreshold;
			float smoothThreshold = _Rtt.SmoothThreshold(Speed);
			float correctionRate = _Rtt.CorrectionRate;

			if (distance < snapThreshold)
				return;

			if (distance < smoothThreshold)
			{
				float t = correctionRate;
				Position = new Vec2(
					Position.X + (serverPos.X - Position.X) * t,
					Position.Z + (serverPos.Z - Position.Z) * t
				);
			}
			else
			{
				Position = serverPos;
				if (IsMoving)
				{
					var corridor = AStarSolver.Solve(_Mesh, Position, Target);
					if (corridor != null)
					{
						var waypoints = FunnelSolver.Solve(_Mesh, corridor, Position, Target);
						_Path = ToArray(waypoints);
						_PathIndex = 0;
					}
					else
					{
						IsMoving = false;
					}
				}
			}

			if (!sync.IsMoving)
				IsMoving = false;
		}

		private static Vec2[] ToArray(System.Collections.Generic.IReadOnlyList<Vec2> list)
		{
			var arr = new Vec2[list.Count];
			for (int i = 0; i < list.Count; i++)
				arr[i] = list[i];
			return arr;
		}
	}
}
