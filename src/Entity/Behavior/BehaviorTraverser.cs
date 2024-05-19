using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class EntityBehaviorTraverser : EntityBehavior {
		public EntityBehaviorTraverser(Entity entity) : base(entity) { }

		public SoldierWaypointsTraverser waypointsTraverser { get; private set; }

		public override string PropertyName() {
			return "SoldierTraverser";
		}

		public override void Initialize(EntityProperties properties, JsonObject attributes) {
			base.Initialize(properties, attributes);
			waypointsTraverser = new SoldierWaypointsTraverser(entity as EntityAgent);
		}

		public override void OnGameTick(float deltaTime) {
			base.OnGameTick(deltaTime);
			waypointsTraverser.OnGameTick(deltaTime);
		}
	}
}