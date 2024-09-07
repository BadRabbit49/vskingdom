using System;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSKingdom;

public class ItemBanner : Item {
	public string pointAnimation => "throwaim";
	public string orderAnimation => "throw";
	private WorldInteraction[] interactions;

	public override void OnLoaded(ICoreAPI api) {
		var bh = GetCollectibleBehavior<CollectibleBehaviorAnimationAuthoritative>(true);
		base.OnLoaded(api);
		if (api.Side == EnumAppSide.Client) {
			interactions = ObjectCacheUtil.GetOrCreate(api, "bowInteractions", () => {
				List<ItemStack> stacks = new List<ItemStack>();
				return new WorldInteraction[] {
					new WorldInteraction() {
						ActionLangCode = "heldhelp-chargebow",
						MouseButton = EnumMouseButton.Right,
						HotKeyCode = "ctrl",
						Itemstacks = stacks.ToArray()
					}
				};
			});
		}
	}

	public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot) {
		return interactions.Append(base.GetHeldInteractionHelp(inSlot));
	}

	public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
		base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
		if (handling == EnumHandHandling.PreventDefault || byEntity.Controls.CtrlKey) {
			return;
		}
		if (byEntity is EntityPlayer player && player.LeftHandItemSlot.Empty && byEntity.WatchedAttributes.HasAttribute("followerEntityUids")) {
			byEntity.AnimManager.ActiveAnimationsByAnimCode["interactstatic"] = new AnimationMetaData {
				Code = pointAnimation,
				Animation = pointAnimation
			};
		}
	}

	public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity) {
		if (forEntity.Attributes.GetInt("aiming") == 1) {
			return pointAnimation;
		}
		return base.GetHeldTpUseAnimation(activeHotbarSlot, forEntity);
	}

	public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
		return true;
	}

	public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason) {
		(byEntity.Api as ICoreClientAPI)?.ShowChatMessage("Canceled interact for reason: " + cancelReason.ToString());
		byEntity.Attributes.SetInt("aiming", 0);
		byEntity.AnimManager.StopAnimation(pointAnimation);
		if (cancelReason != EnumItemUseCancelReason.ReleasedMouse) {
			byEntity.Attributes.SetInt("aimingCancel", 1);
		}
		if (cancelReason == EnumItemUseCancelReason.ReleasedMouse) {
			this.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
		}
		byEntity.AnimManager.ActiveAnimationsByAnimCode["interactstatic"] = new AnimationMetaData {
			Code = "interactstatic",
			Animation = "interactstatic",
			AnimationSpeed = 1,
			EaseInSpeed = 10,
			EaseOutSpeed = 6,
			BlendMode = EnumAnimationBlendMode.Add,
			ElementWeight = new Dictionary<string, float>() {
				{ "UpperArmR", 8.0f },
				{ "LowerArmR", 8.0f },
				{ "UpperArmL", 0.2f },
				{ "LowerArmL", 0.2f }
			},
			ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode>() {
				{ "UpperArmR", EnumAnimationBlendMode.AddAverage },
				{ "LowerArmR", EnumAnimationBlendMode.AddAverage },
				{ "UpperArmL", EnumAnimationBlendMode.AddAverage },
				{ "LowerArmL", EnumAnimationBlendMode.AddAverage }
			}
		};
		return true;
	}

	public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
		if (byEntity is not EntityPlayer || byEntity.Attributes.GetInt("aimingCancel") == 1) {
			byEntity.Attributes.SetInt("aiming", 0);
			byEntity.AnimManager.StopAnimation(pointAnimation);
			base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
			return;
		}
		Vec3d lookAtPos = byEntity.ServerPos.XYZ.AddCopy(byEntity.LocalEyePos).AheadCopy(150, byEntity.ServerPos.HeadPitch, byEntity.ServerPos.HeadYaw);
		byEntity.World.RayTraceForSelection(byEntity.ServerPos.XYZ.AddCopy(byEntity.LocalEyePos), lookAtPos, ref blockSel, ref entitySel);
		if ((entitySel == null && blockSel == null) || byEntity?.World == null || !byEntity.WatchedAttributes.HasAttribute("followerEntityUids")) {
			byEntity.Attributes.SetInt("aiming", 0);
			byEntity.AnimManager.StopAnimation(pointAnimation);
			return;
		}
		Random rand = byEntity.World.Rand;
		float HorRadius = 0;
		float VerRadius = 0;
		bool charingBlockPos = false;
		bool attackingEntity = false;
		if (blockSel != null) {
			HorRadius = byEntity.ServerPos.XYZ.DistanceTo(blockSel.Position.ToVec3d());
			VerRadius = HorRadius / 2;
			charingBlockPos = true;
			attackingEntity = false;
			(byEntity.Api as ICoreClientAPI)?.ShowChatMessage($" BlocksPos is [{blockSel.Position.X}, {blockSel.Position.Y}, {blockSel.Position.Z}]");
		}
		if (entitySel != null) {
			charingBlockPos = false;
			attackingEntity = IsTargetableEntity((byEntity as EntityPlayer), entitySel.Entity);
			(byEntity.Api as ICoreClientAPI)?.ShowChatMessage($" EntityPos is [{entitySel.Position.X}, {entitySel.Position.Y}, {entitySel.Position.Z}]");
		}
		if (!attackingEntity && !charingBlockPos) {
			byEntity.Attributes.SetInt("aiming", 0);
			byEntity.AnimManager.StopAnimation(pointAnimation);
			return;
		}
		// Order the following troops to charge at that block! Expand radius depending on how far away it is.
		byEntity.Attributes.SetInt("aiming", 0);
		byEntity.AnimManager.StopAnimation(pointAnimation);
		byEntity.AnimManager.StartAnimation(orderAnimation);
		(api as ICoreClientAPI)?.World.AddCameraShake(0.05f);
		List<long> followersIDs = (byEntity.WatchedAttributes.GetAttribute("followerEntityUids") as LongArrayAttribute)?.value.ToList();
		(byEntity.Api as ICoreClientAPI)?.ShowChatMessage($"[{followersIDs[0]}]");
		foreach (long entityId in followersIDs) {
			if (byEntity.World.GetEntityById(entityId) is not EntitySentry) {
				followersIDs.RemoveAll(following => following == entityId);
				continue;
			}
			EntitySentry follower = byEntity.World.GetEntityById(entityId) as EntitySentry;
			// Send to server for entity to update values and stuff.
			if (charingBlockPos) {
				Vec3d randomPosition = new Vec3d(rand.Next(-(int)HorRadius, (int)HorRadius), rand.Next(-(int)VerRadius, (int)VerRadius), rand.Next(-(int)HorRadius, (int)HorRadius));
				(byEntity.World.Api as ICoreClientAPI)?.Network.SendEntityPacket(1500, randomPosition);
			} else if (attackingEntity) {
				(byEntity.World.Api as ICoreClientAPI)?.Network.SendEntityPacket(1501, entitySel.Entity.EntityId);
			}
			if (true) {
				follower.WatchedAttributes.SetLong("guardedEntityId", 0L);
				followersIDs.RemoveAll(following => following == entityId);
			}
		}
		byEntity.WatchedAttributes.SetAttribute("followerEntityUids", new LongArrayAttribute(followersIDs.ToArray()));
	}

	/** THIS IS MEANT TO BE CALLED ON ENTITYSENTRY AT 1500 & 1501 RESPECTIVELY
	public virtual void UpdatePaths(byte[] data) {
		Vec3d position = SerializerUtil.Deserialize<Vec3d>(data) ?? ServerPos.XYZ;
		string curAnimation = new string(cachedData?.moveAnims);
		PathTraverserBase pathTraverser = GetBehavior<EntityBehaviorTaskAI>()?.PathTraverser;
		AnimManager.StartAnimation(new AnimationMetaData() { Animation = curAnimation, Code = curAnimation, MulWithWalkSpeed = true, BlendMode = EnumAnimationBlendMode.Average, EaseInSpeed = 999f, EaseOutSpeed = 1f }.Init());
		// WOW, hope this doesn't break haha...
		pathTraverser.NavigateTo_Async(position, cachedData.moveSpeed, 2, () => {
			AnimManager.StopAnimation(curAnimation);
			pathTraverser.Stop();
		}, () => {
			pathTraverser.Retarget();
		}, () => {
			pathTraverser.Retarget();
			BlockSelection blockSel = null;
			EntitySelection entitySel = null;
			World.RayTraceForSelection(ServerPos.XYZ.AddCopy(LocalEyePos), position, ref blockSel, ref entitySel);
			if (blockSel != null) {
				position = blockSel.HitPosition;
			} else if (entitySel != null) {
				position = entitySel.HitPosition;
			}
			pathTraverser.WalkTowards(blockSel.HitPosition, cachedData.moveSpeed, 2, () => AnimManager.StopAnimation(curAnimation), () => pathTraverser.Retarget());
		});
	}

	public virtual void UpdateEnemy(byte[] data) {
		long targetID = SerializerUtil.Deserialize<long>(data);
		Entity target = World.GetEntityById(targetID);
		if (target != null) {
			GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.GetTask<AiTaskSentryAttack>()?.OnAllyAttacked(target);
			GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.GetTask<AiTaskSentryRanged>()?.OnAllyAttacked(target);
		}
	}**/

	private bool IsTargetableEntity(EntityPlayer player, Entity target) {
		if (target == null || target == player || !target.Alive) {
			return false;
		}
		if (target.WatchedAttributes.HasAttribute("leadersGUID") && player.PlayerUID == target.WatchedAttributes.GetString("leadersGUID")) {
			return false;
		}
		if (target.WatchedAttributes.HasAttribute("kingdomGUID") && player.WatchedAttributes.GetString("kingdomGUID") == target.WatchedAttributes.GetString("kingdomGUID")) {
			return false;
		}
		if (target.WatchedAttributes.HasAttribute("domesticationstatus")) {
			if (!target.WatchedAttributes.GetTreeAttribute("domesticationstatus")?.HasAttribute("owner") ?? false) {
				return true;
			}
			string ownerGUID = target.WatchedAttributes.GetTreeAttribute("domesticationstatus")?.GetString("owner");
			if (ownerGUID != null && !target.WatchedAttributes.HasAttribute("leadersGUID")) {
				target.WatchedAttributes.SetString("leadersGUID", ownerGUID);
			}
			if (player.PlayerUID == ownerGUID) {
				return false;
			}
			return false;
		}
		return true;
	}
}