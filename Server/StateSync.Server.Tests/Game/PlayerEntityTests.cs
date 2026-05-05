namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using Pathfinding.Data;
using Pathfinding.Algorithm;

public class PlayerEntityTests
{
	private static NavMesh2D BuildSimpleMesh()
	{
		// Simple square mesh: two triangles forming a 10x10 square
		var vertices = new Vec2[]
		{
			new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f)
		};
		var triangles = new NavTriangle[]
		{
			new(0, 0, 1, 2, 1, -1, -1),
			new(1, 0, 2, 3, -1, -1, 0)
		};
		return new NavMesh2D(vertices, triangles);
	}

	[Fact]
	public void SetDestination_ValidTarget_ReturnsTrue()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);

		bool result = entity.SetDestination(new Vec2(9f, 9f));

		Assert.True(result);
		Assert.True(entity.IsMoving);
	}

	[Fact]
	public void SetDestination_TargetOutsideMesh_ReturnsFalse()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);

		bool result = entity.SetDestination(new Vec2(100f, 100f));

		Assert.False(result);
		Assert.False(entity.IsMoving);
	}

	[Fact]
	public void Tick_MovesAlongPath()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
		entity.SetDestination(new Vec2(9f, 1f));

		entity.Tick(0.1f); // 5 * 0.1 = 0.5 units

		float distance = entity.Position.DistanceTo(new Vec2(1f, 1f));
		Assert.True(distance > 0.4f);
		Assert.True(entity.IsMoving);
	}

	[Fact]
	public void Tick_ReachesDestination_StopsMoving()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 100f, mesh);
		entity.SetDestination(new Vec2(2f, 1f));

		entity.Tick(1f); // 100 * 1 = 100 units, far exceeds path length

		Assert.False(entity.IsMoving);
	}

	[Fact]
	public void Tick_WhenNotMoving_DoesNothing()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
		var posBefore = entity.Position;

		entity.Tick(0.1f);

		Assert.Equal(posBefore, entity.Position);
	}
}
