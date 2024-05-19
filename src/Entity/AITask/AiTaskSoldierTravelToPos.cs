using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using System;

namespace VSKingdom {
	public class AiTaskSoldierTravelToPos : AiTaskBase {
		public AiTaskSoldierTravelToPos(EntityAgent entity) : base(entity) { }

		float moveSpeed = 0.04f;
		float targetDistance = 0.5f;

		bool done;

		int searchDepth = 5000;

		public Vec3d MainTarget;

		protected SoldierWaypointsTraverser soldierPathTraverser;

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			if (taskConfig["movespeed"] != null) {
				moveSpeed = taskConfig["movespeed"].AsFloat(1f);
			}
			soldierPathTraverser = entity.GetBehavior<EntityBehaviorTraverser>().waypointsTraverser;
		}

		private double moveDownToFloor(int x, double y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				Block block = world.BlockAccessor.GetBlock(new BlockPos(x, (int)y, z, 0));
				if (block.SideSolid[BlockFacing.UP.Index]) {
					return y + 1;
				}
				y--;
			}
			return -1;
		}

		public override bool ShouldExecute() {
			return MainTarget != null;
		}

		public override void StartExecute() {
			base.StartExecute();
			done = false;
			bool ok = soldierPathTraverser.NavigateTo(MainTarget, moveSpeed, targetDistance, OnGoalReached, OnStuck, true, searchDepth);
			var sapi = entity.Api as ICoreServerAPI;
		}

		public override bool ContinueExecute(float dt) {
			if (MainTarget is null) {
				return false;
			}
			if (entity.Controls.IsClimbing && entity.Properties.CanClimbAnywhere && entity.ClimbingOnFace != null) {
				BlockFacing facing = entity.ClimbingOnFace;
				if (Math.Sign(facing.Normali.X) == Math.Sign(soldierPathTraverser.CurrentTarget.X - entity.ServerPos.X)) {
					soldierPathTraverser.CurrentTarget.X = entity.ServerPos.X;
				}
				if (Math.Sign(facing.Normali.Z) == Math.Sign(soldierPathTraverser.CurrentTarget.Z - entity.ServerPos.Z)) {
					soldierPathTraverser.CurrentTarget.Z = entity.ServerPos.Z;
				}
			}
			if (MainTarget.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z) < 0.5) {
				soldierPathTraverser.Stop();
				MainTarget = null;
				return false;
			}
			return !done;
		}

		public override void FinishExecute(bool cancelled) {
			base.FinishExecute(cancelled);
			if (cancelled) {
				soldierPathTraverser.Stop();
			} else {
				MainTarget = null;
			}
			StopAnimations();
		}

		private void OnStuck() {
			done = true;
			MainTarget = null;
		}

		private void OnGoalReached() {
			done = true;
			MainTarget = null;
		}

		private void StopAnimations() {
			try {
				if (entity.AnimManager.IsAnimationActive("walk")) {
					entity.StopAnimation("walk");
					return;
				}
				if (entity.AnimManager.IsAnimationActive("sprint")) {
					entity.StopAnimation("sprint");
					return;
				}
				if (entity.AnimManager.IsAnimationActive("swim")) {
					entity.StopAnimation("swim");
					return;
				}
			} catch {
				// Do nothing, whatever.
			}
		}
	}
}