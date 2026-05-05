namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using Pathfinding.Data;

public class MoveControllerTests
{
	private static NavMesh2D BuildSimpleMesh()
	{
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
	public void StartMove_ValidTarget_AddsToMoving()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
		var controller = new MoveController();

		bool result = controller.StartMove(entity, new Vec2(9f, 9f));

		Assert.True(result);
		Assert.True(controller.HasMovingEntities);
	}

	[Fact]
	public void StartMove_InvalidTarget_ReturnsFalse()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
		var controller = new MoveController();

		bool result = controller.StartMove(entity, new Vec2(100f, 100f));

		Assert.False(result);
		Assert.False(controller.HasMovingEntities);
	}

	[Fact]
	public void Tick_EntityReachesDestination_RemovesFromMoving()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 100f, mesh);
		var controller = new MoveController();
		controller.StartMove(entity, new Vec2(2f, 1f));

		controller.Tick(1f);

		Assert.False(controller.HasMovingEntities);
	}

	[Fact]
	public void Tick_EntityStillMoving_RemainsInSet()
	{
		var mesh = BuildSimpleMesh();
		var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 1f, mesh);
		var controller = new MoveController();
		controller.StartMove(entity, new Vec2(9f, 1f));

		controller.Tick(0.1f);

		Assert.True(controller.HasMovingEntities);
	}

	[Fact]
	public void GetMovingEntities_ReturnsCurrentlyMoving()
	{
		var mesh = BuildSimpleMesh();
		var e1 = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
		var e2 = new PlayerEntity("p2", new Vec2(2f, 2f), 5f, mesh);
		var controller = new MoveController();
		controller.StartMove(e1, new Vec2(9f, 1f));
		controller.StartMove(e2, new Vec2(9f, 2f));

		var moving = controller.GetMovingEntities();

		Assert.Equal(2, moving.Count);
	}
}
