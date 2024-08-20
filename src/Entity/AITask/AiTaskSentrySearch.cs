using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Vintagestory.API.Config;

namespace VSKingdom {
	public class AiTaskSentrySearch : AiTaskBaseTargetable {
		public AiTaskSentrySearch(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool archerPilled;
		protected bool cancelSearch;
		protected bool lastPathfind;
		protected long lastAttackedAtMs;
		protected long lastHurtByTarget;
		protected long lastFinishedAtMs;
		protected long lastRetreatsAtMs;
		protected float extraTargetOffset;
		protected float currentUpdateTime;
		protected float currentFollowTime;
		protected float maximumFollowTime;
		protected float maximumRange;
		protected float retreatRange;
		protected float seekingRange;
		protected float curMoveSpeed;
		protected string curAnimation;
		protected Vec3d lastGoalReachedPos;
		protected Vec3d targetPos;
		protected Dictionary<long, int> futilityCounters;
		protected EnumAttackPattern attackPattern;
		protected bool InTheReachOfTargets => targetEntity.ServerPos.SquareDistanceTo(entity.ServerPos.XYZ) < (extraTargetOffset * extraTargetOffset);
		protected bool RecentlyTookDamages => entity.World.ElapsedMilliseconds - lastHurtByTarget < 10000;
		protected bool RemainInRetreatMode => entity.World.ElapsedMilliseconds - lastRetreatsAtMs < 20000;
		protected bool RemainInOffenseMode => entity.World.ElapsedMilliseconds - lastAttackedAtMs < 20000;
		protected float pursueRange { get => entity.WatchedAttributes.GetFloat("pursueRange", 1f); }
		protected Vec3d outpostXYZD { get => entity.WatchedAttributes.GetBlockPos("postBlock").ToVec3d(); }

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
			this.mincooldown = taskConfig["mincooldown"].AsInt(1000);
			this.maxcooldown = taskConfig["maxcooldown"].AsInt(1500);
			this.retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);
			this.extraTargetOffset = taskConfig["extraTargetOffset"].AsFloat(1f);
			this.maximumFollowTime = taskConfig["maximumFollowTime"].AsFloat(60f);
			this.retreatRange = taskConfig["retreatRange"].AsFloat(36f);
			this.seekingRange = taskConfig["seekingRange"].AsFloat(25f);
			this.curMoveSpeed = taskConfig["curMoveSpeed"].AsFloat(0.03f);
			this.skipEntityCodes = taskConfig["skipEntityCodes"].AsArray<string>()?.Select((string str) => AssetLocation.Create(str, entity.Code.Domain)).ToArray();
			this.archerPilled = taskConfig["isArcher"].AsBool(false);
			if (archerPilled) {
				extraTargetOffset *= 5f;
			}
		}

		public override bool ShouldExecute() {
			return targetEntity != null && (targetEntity?.Alive ?? false);
		}

		public override void StartExecute() {
			cancelSearch = false;
			currentFollowTime = 0f;
			maximumRange = pursueRange * pursueRange;
			targetPos = targetEntity.ServerPos.XYZ;
			if (RemainInRetreatMode) {
				Retreats();
				return;
			}
			var navigateAction = DoSieged;
			if (archerPilled) {
				bool safeSpot = !NotSafe();
				bool runfight = !safeSpot && world.Rand.NextDouble() < 0.5;
				navigateAction = runfight ? Retreats : DoCircle;
			} else {
				navigateAction = DoDirect;
			}
			navigateAction();
		}

		public override bool CanContinueExecute() {
			if (targetEntity == null) {
				cancelSearch = true;
				StopAnimation();
				return false;
			}
			if (pathTraverser.Ready) {
				lastAttackedAtMs = entity.World.ElapsedMilliseconds;
				lastPathfind = true;
				return true;
			} else {
				return attackPattern == EnumAttackPattern.TacticalRetreat;
			}
		}

		public override bool ContinueExecute(float dt) {
			if (cancelSearch || !targetEntity.Alive) {
				return false;
			}
			if (currentFollowTime == 0f && world.Rand.NextDouble() < 0.25) {
				base.StartExecute();
			}
			if (archerPilled && InTheReachOfTargets) {
				attackPattern = EnumAttackPattern.TacticalRetreat;
			}
			if (!entity.ruleOrder[3]) {
				if (entity.ruleOrder[1] && entity.ServerPos.SquareDistanceTo(entity.World.GetEntityById(entity.WatchedAttributes.GetLong("guardedEntityId", 0L)).ServerPos) > maximumRange) {
					return false;
				} else if (entity.ServerPos.SquareDistanceTo(outpostXYZD) > maximumRange) {
					return false;
				}
			}
			retreatRange = Math.Max(20f, retreatRange - dt / 4f);
			currentFollowTime += dt;
			currentUpdateTime += dt;
			MoveAnimation();
			if (attackPattern != EnumAttackPattern.TacticalRetreat) {
				if (RecentlyTookDamages && (!lastPathfind || IsInEmotionState("fleeondamage"))) {
					Retreats();
				}
				if (attackPattern == EnumAttackPattern.DirectAttack && currentUpdateTime >= 0.75f && targetPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) >= 9f) {
					targetPos.Set(targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 10.0, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 10.0);
					MoveAnimation();
					pathTraverser.WalkTowards(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck);
					currentUpdateTime = 0f;
				}
				if (attackPattern == EnumAttackPattern.DirectAttack || attackPattern == EnumAttackPattern.BesiegeTarget) {
					pathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
					pathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
					pathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;
				}
			} else if (attackPattern == EnumAttackPattern.TacticalRetreat && world.Rand.NextDouble() < 0.2) {
				updateTargetPosFleeMode(targetPos);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
				MoveAnimation();
			}
			if (pathTraverser.Active) {
				// Look ahead, if there is a door, open it if possible before walking through. Remember to shut afterwards if it was closed! Code courtesy of Dana!
				entity.World.BlockAccessor.WalkBlocks(entity.ServerPos.AsBlockPos.AddCopy(-1, -1, -1), entity.ServerPos.AsBlockPos.AddCopy(1, 1, 1), (block, x, y, z) => {
					BlockPos pos = new(x, y, z, entity.SidedPos.Dimension);
					TryOpen(pos);
				});
			}
			Vec3d vec3 = entity.ServerPos.XYZ.Add(0.0, entity.SelectionBox.Y2 / 2f, 0.0).Ahead(entity.SelectionBox.XSize / 2f, 0f, entity.ServerPos.Yaw);
			double dist = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.XYZ).ShortestDistanceFrom(vec3);
			bool flag = targetEntity != null && targetEntity.Alive && !cancelSearch && pathTraverser.Active;
			if (attackPattern == EnumAttackPattern.TacticalRetreat) {
				if (flag && currentFollowTime < 9f) {
					return dist < retreatRange;
				}
				return false;
			}
			if (flag && currentFollowTime < maximumFollowTime && dist < seekingRange) {
				if (!(dist > TargetDist())) {
					if (targetEntity is EntityAgent entityAgent) {
						return entityAgent?.ServerControls?.TriesToMove ?? false;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			lastFinishedAtMs = entity.World.ElapsedMilliseconds;
			if (targetEntity == null || !targetEntity.Alive) {
				StopAnimation();
				targetEntity = null;
				targetPos = null;
				cancelSearch = true;
			}
		}

		public override bool Notify(string key, object data) {
			if (key == "seekEntity" && data != null) {
				targetEntity = (Entity)data;
				targetPos = targetEntity.ServerPos.XYZ;
				return true;
			}
			return false;
		}

		public void SetTargetEnts(Entity target) {
			targetEntity = target;
			targetPos = target?.ServerPos.XYZ ?? targetPos;
		}

		public void HoldingRange() {
			StopAnimation();
		}

		public void TargetKilled() {
			FinishExecute(true);
			StopAnimation();
		}

		private void DoDirect() {
			int searchDepth = (world.Rand.NextDouble() < 0.05) ? 10000 : 3500;
			attackPattern = EnumAttackPattern.DirectAttack;
			MoveAnimation();
			pathTraverser.NavigateTo_Async(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck, DoSieged, searchDepth, 1);
		}

		private void DoSieged() {
			// Unable to perform direct attack pattern, trying sieged!
			attackPattern = EnumAttackPattern.BesiegeTarget;
			MoveAnimation();
			pathTraverser.NavigateTo_Async(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck, DoCircle, 3500, 3);
		}

		private void DoCircle() {
			// Unable to perform sieged attack pattern, trying circle!
			if (targetPos.DistanceTo(entity.ServerPos.XYZ) > seekingRange) {
				Retreats();
				return;
			}
			attackPattern = EnumAttackPattern.CircleTarget;
			lastPathfind = false;
			float num1 = (float)Math.Atan2(entity.ServerPos.X - targetPos.X, entity.ServerPos.Z - targetPos.Z);
			for (int i = 0; i < 3; i++) {
				double value = (double)num1 + 0.5 + world.Rand.NextDouble() / 2.0;
				double num2 = 4.0 + world.Rand.NextDouble() * 6.0;
				double x = GameMath.Sin(value) * num2;
				double z = GameMath.Cos(value) * num2;
				targetPos.Add(x, 0.0, z);
				int num3 = 0;
				bool flag = false;
				BlockPos blockPos = new BlockPos((int)targetPos.X, (int)targetPos.Y, (int)targetPos.Z, targetPos.AsBlockPos.dimension);
				int num4 = 0;
				while (num3 < 5) {
					if (world.BlockAccessor.GetBlock(new BlockPos(blockPos.X, blockPos.Y - num4, blockPos.Z, blockPos.dimension)).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d((double)blockPos.X + 0.5, blockPos.Y - num4 + 1, (double)blockPos.Z + 0.5), alsoCheckTouch: false)) {
						flag = true;
						targetPos.Y -= num4;
						targetPos.Y += 1.0;
						break;
					}
					if (world.BlockAccessor.GetBlock(new BlockPos(blockPos.X, blockPos.Y + num4, blockPos.Z, blockPos.dimension)).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d((double)blockPos.X + 0.5, blockPos.Y + num4 + 1, (double)blockPos.Z + 0.5), alsoCheckTouch: false)) {
						flag = true;
						targetPos.Y += num4;
						targetPos.Y += 1.0;
						break;
					}
					num3++;
					num4++;
				}
				if (flag) {
					MoveAnimation();
					pathTraverser.NavigateTo_Async(targetPos, curMoveSpeed, TargetDist(), OnGoals, OnStuck, Retreats, 3500, 1);
					return;
				}
			}
			Retreats();
		}

		private void Retreats() {
			// Unable to perform circle attack pattern, trying retreat!
			if (!RemainInOffenseMode && (RecentlyTookDamages || RemainInRetreatMode)) {
				updateTargetPosFleeMode(targetPos);
				pathTraverser.CurrentTarget.X = targetPos.X;
				pathTraverser.CurrentTarget.Y = targetPos.Y;
				pathTraverser.CurrentTarget.Z = targetPos.Z;
				MoveAnimation();
				pathTraverser.WalkTowards(targetPos, curMoveSpeed, targetEntity.SelectionBox.XSize + 0.2f, OnGoals, OnStuck);
				if (attackPattern != EnumAttackPattern.TacticalRetreat) {
					lastRetreatsAtMs = entity.World.ElapsedMilliseconds;
				}
				attackPattern = EnumAttackPattern.TacticalRetreat;
				attackedByEntity = null;
			}
		}

		private void OnStuck() {
			cancelSearch = true;
			StopAnimation();
		}

		private void OnGoals() {
			if (attackPattern != 0 && attackPattern != EnumAttackPattern.BesiegeTarget) {
				return;
			}
			if (lastGoalReachedPos != null && lastGoalReachedPos.SquareDistanceTo(entity.ServerPos) < 0.005f) {
				if (futilityCounters == null) {
					futilityCounters = new Dictionary<long, int>();
				} else {
					futilityCounters.TryGetValue(targetEntity.EntityId, out var value);
					value++;
					futilityCounters[targetEntity.EntityId] = value;
					if (value > 19) {
						return;
					}
				}
			}
			lastGoalReachedPos = new Vec3d(entity.Pos);
			pathTraverser.Retarget();
			StopAnimation();
		}

		private void MoveAnimation() {
			if (cancelSearch) {
				curMoveSpeed = 0;
				StopAnimation();
				return;
			}
			double distance = entity.ServerPos.SquareDistanceTo(targetPos);
			entity.AnimManager.StopAnimation(curAnimation);
			if (entity.FeetInLiquid && !entity.Swimming) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			} else if (entity.Swimming) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.swimAnims);
			} else if (distance > 81f) {
				curMoveSpeed = entity.cachedData.moveSpeed;
				curAnimation = new string(entity.cachedData.moveAnims);
			} else if (distance > 1f && distance < 81f) {
				curMoveSpeed = entity.cachedData.walkSpeed;
				curAnimation = new string(entity.cachedData.walkAnims);
			} else {
				StopAnimation();
				return;
			}
			entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 1f }.Init());
		}

		private void StopAnimation() {
			curMoveSpeed = 0;
			if (curAnimation != null) {
				entity.AnimManager.StopAnimation(curAnimation);
			}
			entity.AnimManager.StopAnimation(new string(entity.cachedData.walkAnims));
			entity.AnimManager.StopAnimation(new string(entity.cachedData.moveAnims));
			entity.AnimManager.StopAnimation(new string(entity.cachedData.swimAnims));
		}

		private bool GoingDirectly(Vec3d pos1, Vec3d pos2) {
			EntitySelection entitySel = new EntitySelection();
			BlockSelection blockSel = new BlockSelection();
			// Line trace to see if blocks are in the way.
			entity.World.RayTraceForSelection(pos1.AddCopy(entity.LocalEyePos), pos2.AddCopy(entity.LocalEyePos), ref blockSel, ref entitySel);
			// If a door is in the way, open it!
			if (blockSel?.Block is BlockBaseDoor || blockSel?.Block is BlockDoor) {
				return !IsLocked(blockSel.Position);
			}
			if (blockSel?.Block.IsLiquid() ?? false) {
				return true;
			}
			return false;
		}

		private bool TryOpen(BlockPos pos) {
			Block block = entity.World.BlockAccessor.GetBlock(pos);
			if (IsLocked(pos)) {
				return false;
			}
			Caller caller = new() { Type = EnumCallerType.Entity, Entity = entity, };
			BlockSelection blockSelection = new(pos, BlockFacing.DOWN, block);
			TreeAttribute activationArgs = new();
			activationArgs.SetBool("opened", true);
			block.Activate(entity.World, caller, blockSelection, activationArgs);
			return true;
		}

		private bool IsLocked(BlockPos pos) {
			ModSystemBlockReinforcement blockReinforcement = entity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
			if (!blockReinforcement.IsReinforced(pos)) {
				return false;
			}
			if (entity.cachedData.leadersGUID == null) {
				return true;
			}
			return blockReinforcement.IsLockedForInteract(pos, world.PlayerByUid(entity.cachedData.leadersGUID));
		}

		private bool NotSafe() {
			if (targetEntity.ServerPos.SquareHorDistanceTo(entity.ServerPos.XYZ) > 16f) {
				return false;
			}
			if (entity.ServerPos.Y - targetEntity.ServerPos.Y > 4) {
				return false;
			}
			bool noCrossing = false;
			float angleHor = (float)Math.Atan2(targetEntity.ServerPos.X, targetEntity.ServerPos.Z) + GameMath.PIHALF;
			Vec3d blockAhead = targetEntity.ServerPos.XYZ.Ahead(1, 0, angleHor);
			Vec3d startAhead = entity.ServerPos.XYZ.Ahead(1, 0, angleHor);
			GameMath.BresenHamPlotLine2d((int)startAhead.X, (int)startAhead.Z, (int)blockAhead.X, (int)blockAhead.Z, (x, z) => {
				int nowY = OnFloor(x, (int)startAhead.Y, z);
				// Not more than 4 blocks down.
				if (nowY < 0 || startAhead.Y - nowY > 4) {
					noCrossing = true;
				}
				startAhead.Y = nowY;
			});
			return !noCrossing;
		}

		private int OnFloor(int x, int y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) {
					return y + 1;
				}
				y--;
			}
			return -1;
		}

		private float TargetDist() {
			return extraTargetOffset + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2f + entity.SelectionBox.XSize / 4f);
		}
	}
}