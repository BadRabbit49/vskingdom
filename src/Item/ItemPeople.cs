using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VSKingdom;
public class ItemPeople : Item {
	public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity) {
		return null;
	}

	public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling) {
		if (blockSel is null) {
			return;
		}
		IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
		if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) {
			return;
		}
		if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative) {
			slot.TakeOut(1);
			slot.MarkDirty();
		}
		AssetLocation assetLocation = new AssetLocation(Code.Domain, CodeEndWithoutParts(1));
		EntityProperties entityType = byEntity.World.GetEntityType(assetLocation);
		if (entityType is null) {
			byEntity.World.Logger.Error("ItemPeople: No such entity - {0}", assetLocation);
			if (api.World.Side == EnumAppSide.Client) {
				(api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", $"No such entity loaded - '{assetLocation}'.");
			}
			return;
		}
		byEntity.World.Logger.Notification("Creating a new entity with code: " + CodeEndWithoutParts(1));
		Entity entity = byEntity.World.ClassRegistry.CreateEntity(entityType);
		if (entity is null) {
			return;
		}
		entity.ServerPos.X = (float)(blockSel.Position.X + ((!blockSel.DidOffset) ? blockSel.Face.Normali.X : 0)) + 0.5f;
		entity.ServerPos.Y = blockSel.Position.Y + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Y : 0);
		entity.ServerPos.Z = (float)(blockSel.Position.Z + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Z : 0)) + 0.5f;
		entity.ServerPos.Yaw = byEntity.Pos.Yaw + MathF.PI + MathF.PI / 2f;
		entity.Pos.SetFrom(entity.ServerPos);
		entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
		entity.Attributes.SetString("origin", "playerplaced");
		JsonObject attributes = Attributes;
		if (attributes != null && attributes.IsTrue("setGuardedEntityAttribute")) {
			entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
			if (byEntity is EntityPlayer entityPlayer) {
				entity.WatchedAttributes.SetString("guardedPlayerUid", entityPlayer.PlayerUID);
			}
		}

		// SPAWNING ENTITY!
		byEntity.World.SpawnEntity(entity);

		try {
			// If the byEntity is a player then make them the assigned leader.
			if (byEntity is EntityPlayer playerEnt) {
				entity.GetBehavior<EntityBehaviorLoyalties>().leadersGUID = playerEnt?.PlayerUID ?? null;
				entity.GetBehavior<EntityBehaviorLoyalties>().cultureGUID = byEntity.GetBehavior<EntityBehaviorLoyalties>()?.cultureGUID ?? "00000000";
				entity.GetBehavior<EntityBehaviorLoyalties>().kingdomGUID = byEntity.GetBehavior<EntityBehaviorLoyalties>()?.kingdomGUID ?? "00000000";
			}
			// If placed on a brazier soldier post then set that to be their outpost.
			if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityPost outpost) {
				outpost.IgnitePost();
				outpost.IsCapacity(entity.EntityId);
				entity.GetBehavior<EntityBehaviorLoyalties>().SetOutpost(blockSel.Position);
			}
		} catch (NullReferenceException) { }
		handHandling = EnumHandHandling.PreventDefaultAction;
	}

	public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity byEntity, EnumHand hand) {
		EntityProperties entityType = byEntity.World.GetEntityType(new AssetLocation(Code.Domain, CodeEndWithoutParts(1)));
		if (entityType is null) {
			return base.GetHeldTpIdleAnimation(activeHotbarSlot, byEntity, hand);
		}
		if (Math.Max(entityType.CollisionBoxSize.X, entityType.CollisionBoxSize.Y) > 1f) {
			return "holdunderarm";
		}
		return "holdbothhands";
	}

	public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot) {
		return new WorldInteraction[1] { new WorldInteraction { ActionLangCode = "heldhelp-place", MouseButton = EnumMouseButton.Right } }.Append(base.GetHeldInteractionHelp(inSlot));
	}

	public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
		base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
	}
}