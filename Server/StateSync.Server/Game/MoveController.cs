namespace StateSync.Server.Game;

using System.Collections.Generic;
using Pathfinding.Data;

public class MoveController
{
	private readonly HashSet<PlayerEntity> _MovingEntities = [];
	private readonly List<PlayerEntity> _ToRemove = [];

	public bool HasMovingEntities => _MovingEntities.Count > 0;

	public bool StartMove(PlayerEntity entity, Vec2 target)
	{
		if (!entity.SetDestination(target))
			return false;

		_MovingEntities.Add(entity);
		return true;
	}

	public void StopMove(PlayerEntity entity)
	{
		_MovingEntities.Remove(entity);
	}

	public void Tick(float deltaTime)
	{
		_ToRemove.Clear();
		foreach (var entity in _MovingEntities)
		{
			entity.Tick(deltaTime);
			if (!entity.IsMoving)
				_ToRemove.Add(entity);
		}
		foreach (var entity in _ToRemove)
			_MovingEntities.Remove(entity);
	}

	public IReadOnlyCollection<PlayerEntity> GetMovingEntities() => _MovingEntities;
}
