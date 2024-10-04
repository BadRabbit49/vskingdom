using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class EntityBehaviorNavigated : EntityBehavior {
		public EntityBehaviorNavigated(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		public bool isClimbingUp;
		public bool isOpenedDoor;
		public bool blockNotNull;
		public long cooldownAtMs;
		public Block currentBlock;
		public Vec3i currentBlPos;
		public Vec3d temporaryPos;
		public Vec3d actualSetPos;
		protected PathTraverserBase pathTraverser;

		public override string PropertyName() {
			return "SoldierNavigated";
		}

		public override void AfterInitialized(bool onFirstSpawn) {
			base.AfterInitialized(onFirstSpawn);
			pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
		}

		public override void OnGameTick(float dt) {
			if (entity.World.ElapsedMilliseconds < cooldownAtMs) {
				return;
			}
			cooldownAtMs = entity.World.ElapsedMilliseconds + 5000;
			if (entity.Alive && pathTraverser != null && pathTraverser.Active) {
				EntitySelection eSelect = new EntitySelection();
				BlockSelection bSelect = new BlockSelection();
				BlockFilter bFilter = (pos, block) => (block != null || block.Replaceable < 6000);
				entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(0, 0.5, 0), pathTraverser.CurrentTarget?.AddCopy(0, 0.5, 0), ref bSelect, ref eSelect, bFilter);
				if (bSelect == null) {
					return;
				}
				blockNotNull = bSelect.Block != null;
				if (isClimbingUp) {
					TryGoUp(bSelect);
				}
				if (isOpenedDoor) {
					TryShut(bSelect);
				}
				if (!blockNotNull) {
					return;
				}
				// DEBUG INFO
				try {
					entity.World.Logger.Notification($"{entity.EntityId} is looking at block: {bSelect?.Block?.Code?.ToString() ?? "null"}");
				} catch {
					entity.World.Logger.Error($"Can't get block code.");
				} 
				if (IsADoor(bSelect)) {
					TryDoor(bSelect);
					return;
				}
				if (IsClimb(bSelect)) {
					TryGoUp(bSelect);
					return;
				}
			}
		}
		
		private bool IsClimb(BlockSelection bSelect) {
			return bSelect.Block.Climbable;
		}
		
		private bool IsADoor(BlockSelection bSelect) {
			return bSelect.Block is BlockDoor || bSelect.Block is BlockBaseDoor || bSelect.Block is BlockTrapdoor;
		}

		private void TryGoUp(BlockSelection bSelect) {
			if (!blockNotNull) {
				isClimbingUp = false;
				return;
			}
			int height = pathTraverser.CurrentTarget.YInt - ((int)entity.ServerPos.Y);
			string animation = (height > 0) ? "ladderup" : "ladderdown";
			entity.ClimbingIntoFace = bSelect.Face;
			temporaryPos = bSelect.Position.UpCopy(height).ToVec3d();
			actualSetPos = pathTraverser.CurrentTarget.Clone();
			pathTraverser.Stop();
			pathTraverser.WalkTowards(temporaryPos, entity.cachedData.walkSpeed, 0.5f, EndGoUp, RedoNav);
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = animation, Code = animation, BlendMode = EnumAnimationBlendMode.Average, MulWithWalkSpeed = true, EaseOutSpeed = 999f }.Init());
		}

		private void TryDoor(BlockSelection bSelect) {
			entity.World.BlockAccessor.WalkBlocks(entity.SidedPos.AsBlockPos.AddCopy(-1, -1, -1), entity.SidedPos.AsBlockPos.AddCopy(1, 1, 1), (block, x, y, z) => {
				BlockPos pos = new(x, y, z, entity.SidedPos.Dimension);
				TryOpen(pos);
			});
			isOpenedDoor = (currentBlock != null && !currentBlPos.IsZero);
		}

		private bool TryOpen(BlockPos pos) {
			Block block = entity.World.BlockAccessor.GetBlock(pos);
			if (block is not BlockBaseDoor || TryLock(pos)) {
				return false;
			}
			BlockSelection blockSelection = new(pos, BlockFacing.DOWN, block);
			(block as BlockBaseDoor)?.OnBlockInteractStart(entity.World, entity.World.PlayerByUid(entity.cachedData.leadersGUID), blockSelection);
			currentBlPos = pos.ToVec3i().Clone();
			currentBlock = block;
			return true;
		}

		private bool TryShut(BlockSelection bSelect) {
			if (currentBlock != null && ((currentBlock is BlockBaseDoor _base && _base.IsOpened()) || currentBlock is BlockBaseDoor _door && _door.IsOpened())) {
				if (TryLock(currentBlPos.AsBlockPos)) {
					return false;
				}
				BlockSelection blockSelection = new(currentBlPos.AsBlockPos, BlockFacing.DOWN, currentBlock);
				(currentBlock as BlockBaseDoor)?.OnBlockInteractStart(entity.World, entity.World.PlayerByUid(entity.cachedData.leadersGUID), blockSelection);
				currentBlPos = new Vec3i(0, 0, 0);
				currentBlock = null;
				return true;
			}
			return false;
		}

		private bool TryLock(BlockPos pos) {
			ModSystemBlockReinforcement blockReinforcement = entity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
			if (!blockReinforcement.IsReinforced(pos)) {
				return false;
			}
			if (entity.cachedData.leadersGUID == null) {
				return true;
			}
			return blockReinforcement.IsLockedForInteract(pos, entity.World.PlayerByUid(entity.cachedData.leadersGUID));
		}

		private void RedoNav() {
			pathTraverser.NavigateTo_Async(actualSetPos, entity.cachedData.walkSpeed, 0.5f, pathTraverser.OnGoalReached, pathTraverser.OnStuck);
		}

		private void EndGoUp() {
			isClimbingUp = false;
			entity.AnimManager.StopAnimation("ladderup");
		}
	}
}