using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
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
				(api as ICoreClientAPI)?.TriggerIngameError(this, "nosuchentity", $"No such entity loaded - '{assetLocation}'.");
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
		}

		byte[] cultureData = (byEntity.Api as ICoreServerAPI)?.WorldManager.SaveGame.GetData("cultureData");
		List<Culture> cultureList = cultureData is null ? new List<Culture>() : SerializerUtil.Deserialize<List<Culture>>(cultureData);
		Culture byCulture = cultureList.Find(cultureMatch => cultureMatch.CultureGUID == byEntity.WatchedAttributes.GetTreeAttribute("loyalties").GetString("culture_guid"));
		// Setup cultural features.
		if (byCulture != null && byCulture?.CultureGUID != "00000000") {
			Random rnd = new Random();
			// Editing the "skinConfig" tree here and changing it to what we want.
			var entitySkinParts = entity.WatchedAttributes.GetOrAddTreeAttribute("skinConfig").GetOrAddTreeAttribute("appliedParts");
			var entityFullNames = entity.WatchedAttributes.GetOrAddTreeAttribute("nametag");

			string[] skinColors = byCulture.SkinColors.ToArray<string>();
			string[] eyesColors = byCulture.EyesColors.ToArray<string>();
			string[] hairColors = byCulture.HairColors.ToArray<string>();
			string[] hairStyles = byCulture.HairStyles.ToArray<string>();
			string[] hairExtras = byCulture.HairExtras.ToArray<string>();
			string[] faceStyles = byCulture.FaceStyles.ToArray<string>();
			string[] faceBeards = byCulture.FaceBeards.ToArray<string>();
			string[] underwears = new string[] { "breeches", "leotard", "twopiece" };
			string[] expression = new string[] { "angry", "grin", "smirk", "kind", "upset", "neutral", "sad", "serious", "tired", "very-sad" };

			entitySkinParts.SetString("baseskin", skinColors[rnd.Next(0, skinColors.Length - 1)]);
			entitySkinParts.SetString("eyecolor", eyesColors[rnd.Next(0, eyesColors.Length - 1)]);
			entitySkinParts.SetString("haircolor", hairColors[rnd.Next(0, hairColors.Length - 1)]);
			entitySkinParts.SetString("hairbase", hairStyles[rnd.Next(0, hairStyles.Length - 1)]);
			entitySkinParts.SetString("hairextra", hairExtras[rnd.Next(0, hairExtras.Length - 1)]);
			
			if (entity.Code.EndVariant() == "masc") {
				entitySkinParts.SetString("mustache", faceStyles[rnd.Next(0, faceStyles.Length - 1)]);
				entitySkinParts.SetString("beard", faceBeards[rnd.Next(0, faceBeards.Length - 1)]);
				entitySkinParts.SetString("underwear", underwears[rnd.Next(0, 1)]);
			}

			if (entity.Code.EndVariant() == "femm") {
				entitySkinParts.SetString("mustache", "none");
				entitySkinParts.SetString("beard", "none");
				entitySkinParts.SetString("underwear", underwears[rnd.Next(1, 2)]);
			}

			entitySkinParts.SetString("facialexpression", expression[rnd.Next(1, expression.Length - 1)]);

			// Set the first and last names of the entity here.
			string[] mascNames = byCulture.MascFNames.ToArray<string>();
			string[] femmNames = byCulture.FemmFNames.ToArray<string>();
			string[] lastNames = byCulture.CommLNames.ToArray<string>();
			switch (entity.Code.EndVariant()) {
				case "masc": entityFullNames.SetString("name", mascNames[rnd.Next(0, mascNames.Length - 1)]); break;
				case "femm": entityFullNames.SetString("name", femmNames[rnd.Next(0, femmNames.Length - 1)]); break;
				default: entityFullNames.SetString("name", LangUtility.Fuse(mascNames, femmNames)[rnd.Next(0, (mascNames.Length + femmNames.Length) - 2)]); break;
			}
			entityFullNames.SetString("last", lastNames[rnd.Next(0, lastNames.Length - 1)]);
		}

		// If the byEntity is a player then make them the assigned leader.
		if (entity is EntitySentry sentry && byEntity is EntityPlayer playerEnt) {
			entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetString("leaders_guid", playerEnt?.PlayerUID ?? null);
			entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetString("culture_guid", byEntity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("culture_guid") ?? "00000000");
			entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetString("kingdom_guid", byEntity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdom_guid") ?? "00000000");
		}

		// If placed on a brazier soldier post then set that to be their outpost.
		if (api.World.BlockAccessor.GetBlock(blockSel.Position).EntityClass != null) {
			if (api.World.ClassRegistry.GetBlockEntity(api.World.BlockAccessor.GetBlock(blockSel.Position).EntityClass) == typeof(BlockEntityPost)) {
				BlockEntityPost outpost = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPost;
				if (outpost.IsCapacity(entity.EntityId)) {
					outpost.IgnitePost();
					entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetDouble("outpost_size", outpost.areasize);
					entity.WatchedAttributes.GetOrAddTreeAttribute("loyalties").SetBlockPos("outpost_xyzd", blockSel.Position);
				}
			}
		}

		// SPAWNING ENTITY!
		byEntity.World.SpawnEntity(entity);
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