using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class AiTaskSentryWaters : AiTaskBase {
		public AiTaskSentryWaters(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelWaters;
		protected Vec3d target = new Vec3d();
		protected BlockPos pos = new BlockPos(Dimensions.NormalWorld);

		public override bool ShouldExecute() {
			if (!entity.Swimming || rand.NextDouble() > 0.04f) {
				return false;
			}
			target.Y = entity.ServerPos.Y;
			int tries = 6;
			int px = (int)entity.ServerPos.X;
			int pz = (int)entity.ServerPos.Z;
			IBlockAccessor blockAccessor = entity.World.BlockAccessor;
			while (tries-- > 0) {
				pos.X = px + rand.Next(21) - 10;
				pos.Z = pz + rand.Next(21) - 10;
				pos.Y = blockAccessor.GetTerrainMapheightAt(pos);
				Cuboidf[] blockBoxes = blockAccessor.GetBlock(pos).GetCollisionBoxes(blockAccessor, pos);
				pos.Y--;
				Cuboidf[] belowBoxes = blockAccessor.GetBlock(pos).GetCollisionBoxes(blockAccessor, pos);
				if ((blockBoxes == null || blockBoxes.Max((cuboid) => cuboid.Y2) <= 1f) && (belowBoxes != null && belowBoxes.Length > 0)) {
					target.Set(pos.X + 0.5, pos.Y + 1, pos.Z + 0.5);
					return true;
				}
			}
			return false;
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelWaters = false;
			pathTraverser.WalkTowards(target, (float)entity.moveSpeed, 0.5f, OnGoals, OnStuck);
		}

		public override bool ContinueExecute(float dt) {
			if (rand.NextDouble() < 0.1f) {
				if (!entity.FeetInLiquid) {
					return false;
				}
			}
			return !cancelWaters;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			pathTraverser.Stop();
		}

		private void OnStuck() {
			cancelWaters = true;
		}

		private void OnGoals() {
			cancelWaters = true;
		}
	}
}