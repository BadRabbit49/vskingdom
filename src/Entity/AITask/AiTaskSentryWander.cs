using System;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

namespace VSKingdom {
	public class AiTaskSentryWander : AiTaskBase {
		public AiTaskSentryWander(EntitySentry entity) : base(entity) { this.entity = entity; }
		#pragma warning disable CS0108
		public EntitySentry entity;
		#pragma warning restore CS0108
		protected bool cancelWander;
		protected bool doorIsBehind;
		protected long failedWanders;
		protected long lastCheckedMs;
		protected long checkCooldown = 1500L;
		protected float wanderChance;
		protected float wanderHeight;
		protected float targetRanges;
		protected float curMoveSpeed = 0.03f;
		protected Vec3d curTargetPos;
		protected NatFloat wanderRangeHor = NatFloat.createStrongerInvexp(3, 40);
		protected NatFloat wanderRangeVer = NatFloat.createStrongerInvexp(3, 10);

		protected int failedPathfinds {
			get => entity.WatchedAttributes.GetInt("failedConsecutivePathfinds", 0);
			set => entity.WatchedAttributes.SetInt("failedConsecutivePathfinds", value);
		}
		protected float wanderRangedMul { get => entity.WatchedAttributes.GetFloat("wanderRangeMul", 1); }
		protected Vec3d outpostPosition { get => entity.Loyalties.GetBlockPos("outpost_xyzd").ToVec3d(); }
		protected string leaderPlayerUID { get => entity.Loyalties.GetString("leaders_guid"); }

		protected static readonly string walkAnimCode = "walk";
		protected static readonly string moveAnimCode = "move";
		protected static readonly string swimAnimCode = "swim";

		public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig) {
			base.LoadConfig(taskConfig, aiConfig);
			this.targetRanges = taskConfig["targetRanges"].AsFloat(0.12f);
			this.wanderHeight = taskConfig["wanderHeight"].AsFloat(7f);
			this.wanderChance = taskConfig["wanderChance"].AsFloat(0.15f);
			this.whenNotInEmotionState = taskConfig["whenNotInEmotionState"].AsString("aggressiveondamage|fleeondamage");
		}

		public override bool ShouldExecute() {
			if (lastCheckedMs + checkCooldown > entity.World.ElapsedMilliseconds) {
				return false;
			}
			lastCheckedMs = entity.World.ElapsedMilliseconds;
			if (!entity.ruleOrder[0] || entity.ruleOrder[1] || !EmotionStatesSatisifed()) {
				cancelWander = true;
				return false;
			}
			if (rand.NextDouble() > wanderChance) {
				failedWanders = 0;
				return false;
			}
			if (entity.ruleOrder[6] || entity.ServerPos.SquareDistanceTo(outpostPosition) > entity.postRange * entity.postRange) {
				curTargetPos = outpostPosition.Clone();
			} else if (entity.InLava || ((entity.Swimming || entity.FeetInLiquid) && entity.World.Rand.NextDouble() < 0.04f)) {
				curTargetPos = LeaveTheWatersTarget();
			} else {
				curTargetPos = LoadNextWanderTarget();
			}
			return curTargetPos != null;
		}

		public override void StartExecute() {
			base.StartExecute();
			cancelWander = false;
			wanderRangeHor = NatFloat.createInvexp(3f, (float)entity.postRange);
			MoveAnimation();
			bool ok = pathTraverser.WalkTowards(curTargetPos, curMoveSpeed, targetRanges, OnGoals, OnStuck);
		}

		public override bool ContinueExecute(float dt) {
			// If we are a climber dude and encountered a wall, let's not try to get behind the wall.
			// We do that by removing the coord component that would make the entity want to walk behind the wall.
			if (entity.ServerControls.IsClimbing && entity.Properties.CanClimbAnywhere && entity.ClimbingIntoFace != null) {
				BlockFacing facing = entity.ClimbingIntoFace;
				if (Math.Sign(facing.Normali.X) == Math.Sign(pathTraverser.CurrentTarget.X - entity.ServerPos.X)) {
					pathTraverser.CurrentTarget.X = entity.ServerPos.X;
				}
				if (Math.Sign(facing.Normali.Y) == Math.Sign(pathTraverser.CurrentTarget.Y - entity.ServerPos.Y)) {
					pathTraverser.CurrentTarget.Y = entity.ServerPos.Y;
				}
				if (Math.Sign(facing.Normali.Z) == Math.Sign(pathTraverser.CurrentTarget.Z - entity.ServerPos.Z)) {
					pathTraverser.CurrentTarget.Z = entity.ServerPos.Z;
				}
			}
			// If the entity is close enough to the primary target then leave it there.
			if (curTargetPos.HorizontalSquareDistanceTo(entity.ServerPos.X, entity.ServerPos.Z) < 0.5) {
				return false;
			}
			if (cancelWander) {
				return false;
			} else if (world.AllOnlinePlayers.Length > 0 && world.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Entity is IPlayer player && player.Entity.Alive) {
				if (player.Entity.ServerPos.SquareDistanceTo(entity.ServerPos) < 4f && player.Entity.EntitySelection != null && player.Entity.EntitySelection.Entity.EntityId == entity.EntityId) {
					return leaderPlayerUID == player.PlayerUID && player.Entity.ServerControls.RightMouseDown;
				}
			}
			return true;
		}

		public override void FinishExecute(bool cancelled) {
			cooldownUntilMs = entity.World.ElapsedMilliseconds + mincooldown + entity.World.Rand.Next(maxcooldown - mincooldown);
			cooldownUntilTotalHours = entity.World.Calendar.TotalHours + mincooldownHours + entity.World.Rand.NextDouble() * (maxcooldownHours - mincooldownHours);
			StopAnimation();
			pathTraverser.Stop();
			if (entity.ruleOrder[6] && entity.ServerPos.SquareDistanceTo(outpostPosition) < entity.postRange * entity.postRange) {
				HasReturnedTo();
			}
		}

		private Vec3d LeaveTheWatersTarget() {
			Vec3d exitPosition = new Vec3d();
			BlockPos pos = new BlockPos(Dimensions.NormalWorld);
			exitPosition.Y = entity.ServerPos.Y;
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
					exitPosition.Set(pos.X + 0.5, pos.Y + 1, pos.Z + 0.5);
					return exitPosition;
				}
			}
			return null;
		}

		// Requirements:
		// - ✔ Try to not move a lot vertically.
		// - ✔ Try not to fall from very large heights if entity has FallDamageOn.
		// - ✔ Must be above a block the entity can stand on.
		// - ✔ If failed search is high, reduce wander range.
		// - ✘ If wander ranges are not met.
		private Vec3d LoadNextWanderTarget() {
			bool canFallDamage = entity.Api.World.Config.GetAsBool("FallDamageOn");
			int num = 9;
			float rangeMul = wanderRangedMul;
			Vec4d bestTarget = null;
			Vec4d currTarget = new Vec4d();
			if (failedPathfinds > 10) {
				rangeMul = Math.Max(0.1f, wanderRangedMul * 0.9f);
			} else {
				rangeMul = Math.Min(1f, wanderRangedMul * 1.1f);
				if (rand.NextDouble() < 0.05) {
					rangeMul = Math.Min(1f, wanderRangedMul * 1.5f);
				}
			}
			if (rand.NextDouble() < 0.05) {
				rangeMul *= 3f;
			}
			while (num-- > 0) {
				double dx = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * rangeMul;
				double dy = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * rangeMul;
				double dz = wanderRangeHor.nextFloat() * (rand.Next(2) * 2 - 1) * rangeMul;
				currTarget.X = entity.ServerPos.X + dx;
				currTarget.Y = entity.ServerPos.Y + dy;
				currTarget.Z = entity.ServerPos.Z + dz;
				currTarget.W = 1.0;
				currTarget.Y = ToFloor((int)currTarget.X, (int)currTarget.Y, (int)currTarget.Z);
				if (currTarget.Y < 0.0) {
					currTarget.W = 0.0;
				} else {
					if (canFallDamage) {
						// Lets make a straight line plot to see if we would fall off a cliff.
						bool mustStop = false;
						bool willFall = false;
						float angleHor = (float)Math.Atan2(dx, dz) + GameMath.PIHALF;
						Vec3d blockAhead = currTarget.XYZ.Ahead(1, 0, angleHor);
						// Otherwise they are forever stuck if they stand over the edge.
						Vec3d startAhead = entity.ServerPos.XYZ.Ahead(1, 0, angleHor);
						// Draw a line from here to there and check ahead to see if we will fall.
						GameMath.BresenHamPlotLine2d((int)startAhead.X, (int)startAhead.Z, (int)blockAhead.X, (int)blockAhead.Z, (x, z) => {
							if (mustStop) {
								return;
							}
							int nowY = ToFloor(x, (int)startAhead.Y, z);
							// Not more than 4 blocks down.
							if (nowY < 0 || startAhead.Y - nowY > 4) {
								willFall = true;
								mustStop = true;
							}
							// Not more than 2 blocks up.
							if (nowY - startAhead.Y > 2) {
								mustStop = true;
							}
							startAhead.Y = nowY;
						});
						if (willFall) {
							currTarget.W = 0.0;
						}
					}
				}
				if (entity.World.BlockAccessor.GetBlock(new BlockPos((int)currTarget.X, (int)currTarget.Y, (int)currTarget.Z, entity.Pos.Dimension), 2).IsLiquid()) {
					currTarget.W /= 2.0;
				}
				if (currTarget.W > 0.0) {
					for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++) {
						BlockFacing blockFacing = BlockFacing.HORIZONTALS[i];
						if (entity.World.BlockAccessor.IsSideSolid((int)currTarget.X + blockFacing.Normali.X, (int)currTarget.Y, (int)currTarget.Z + blockFacing.Normali.Z, blockFacing.Opposite)) {
							currTarget.W *= 0.5;
						}
					}
				}
				if (bestTarget == null || currTarget.W > bestTarget.W) {
					bestTarget = new Vec4d(currTarget.X, currTarget.Y, currTarget.Z, currTarget.W);
					if (currTarget.W >= 1.0) {
						break;
					}
				}
			}
			if (bestTarget.W > 0.0) {
				failedPathfinds = Math.Max(failedPathfinds - 3, 0);
				return bestTarget.XYZ;
			}
			failedPathfinds++;
			return null;
		}
		
		private void OnStuck() {
			cancelWander = true;
			failedWanders++;
			StopAnimation();
		}

		private void OnGoals() {
			pathTraverser.Retarget();
			failedWanders = 0;
		}

		private void NoPaths() {
			cancelWander = true;
		}

		private int ToFloor(int x, int y, int z) {
			int tries = 5;
			while (tries-- > 0) {
				if (world.BlockAccessor.IsSideSolid(x, y, z, BlockFacing.UP)) {
					return y + 1;
				}
				y--;
			}
			return -1;
		}

		private void HasReturnedTo() {
			entity.ruleOrder[6] = false;
			SentryOrders updatedOrders = new SentryOrders() { entityUID = entity.EntityId, returning = false };
			IServerPlayer nearestPlayer = entity.ServerAPI.World.NearestPlayer(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z) as IServerPlayer;
			entity.ServerAPI?.Network.GetChannel("sentrynetwork").SendPacket<SentryOrders>(updatedOrders, nearestPlayer);
		}

		private void MoveAnimation() {
			if (cancelWander) {
				curMoveSpeed = 0;
				StopAnimation();
			} else if (entity.Swimming) {
				curMoveSpeed = (float)entity.moveSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = swimAnimCode, Code = swimAnimCode, BlendMode = EnumAnimationBlendMode.Average }.Init());
				entity.AnimManager.StopAnimation(walkAnimCode);
				entity.AnimManager.StopAnimation(moveAnimCode);
			} else if (entity.ServerPos.SquareDistanceTo(curTargetPos) > 36f) {
				curMoveSpeed = (float)entity.moveSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = moveAnimCode, Code = moveAnimCode, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average }.Init());
				entity.AnimManager.StopAnimation(walkAnimCode);
				entity.AnimManager.StopAnimation(swimAnimCode);
			} else if (entity.ServerPos.SquareDistanceTo(curTargetPos) > 1f) {
				curMoveSpeed = (float)entity.walkSpeed;
				entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = walkAnimCode, Code = walkAnimCode, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseOutSpeed = 1f }.Init());
				entity.AnimManager.StopAnimation(moveAnimCode);
				entity.AnimManager.StopAnimation(swimAnimCode);
			} else {
				StopAnimation();
			}
		}

		private void StopAnimation() {
			entity.AnimManager.StopAnimation(walkAnimCode);
			entity.AnimManager.StopAnimation(moveAnimCode);
			if (!entity.Swimming) {
				entity.AnimManager.StopAnimation(swimAnimCode);
			}
		}

		/**private void CheckDoors(Vec3d target) {
			if (target != null) {
				// Check if there is a door in the way.
				BlockSelection blockSel = new BlockSelection();
				EntitySelection entitySel = new EntitySelection();

				entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), entity.ServerPos.BehindCopy(4).XYZ, ref blockSel, ref entitySel);
				if (blockSel != null && doorIsBehind && blockSel.Block is BlockBaseDoor rearBlock && rearBlock.IsOpened()) {
					doorIsBehind = ToggleDoor(blockSel.Position);
				}

				entity.World.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), target, ref blockSel, ref entitySel);
				if (blockSel != null && blockSel.Block is BlockBaseDoor baseBlock && !baseBlock.IsOpened()) {
					doorIsBehind = ToggleDoor(blockSel.Position);
				} else if (blockSel.Block is BlockDoor doorBlock && !doorBlock.IsOpened() && doorBlock.IsUpperHalf()) {
					BlockPos realBlock = new BlockPos(blockSel.Position.X, blockSel.Position.Y - 1, blockSel.Position.Z, blockSel.Position.dimension);
					doorIsBehind = ToggleDoor(realBlock);
				}
			}
		}

		private bool ToggleDoor(BlockPos pos) {
			if (pos.HorizontalManhattenDistance(entity.ServerPos.AsBlockPos) > 3) {
				return false;
			}
			if (entity.World.BlockAccessor.GetBlock(pos) is not BlockBaseDoor doorBlock) {
				return false;
			}

			var doorBehavior = BlockBehaviorDoor.getDoorAt(entity.World, pos);
			bool stateOpened = doorBlock.IsOpened();
			bool canBeOpened = TestAccess(pos);

			if (canBeOpened && doorBehavior != null) {
				doorBehavior.ToggleDoorState(null, stateOpened);
				doorBlock.OnBlockInteractStart(entity.World, null, new BlockSelection(pos, BlockFacing.UP, doorBlock));
				return true;
			}
			return false;
		}

		private bool DamageDoor(BlockPos pos) {
			// Break down door over time.
			return false;
		}

		private bool TestAccess(BlockPos pos) {
			if (entity.World.Claims.Get(pos).Length <= 0) {
				return true;
			}
			if (entity.World.Claims.TestAccess(entity.World.PlayerByUid(entity.leadersID), pos, EnumBlockAccessFlags.Use) == EnumWorldAccessResponse.Granted) {
				return true;
			}
			foreach (var claim in entity.World.Claims.Get(pos)) {
				if (entity.World.PlayerByUid(claim.OwnedByPlayerUid)?.Entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") == entity.kingdomID) {
					return true;
				}
			}
			return false;
		}**/
	}
}