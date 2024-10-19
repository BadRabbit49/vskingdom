using System;
using System.Linq;
using System.Text;
using System.Drawing;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static VSKingdom.Utilities.ColoursUtil;
using static VSKingdom.Utilities.GenericUtil;

namespace VSKingdom {
	public class ItemUndead : Item {
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
			EntityProperties properties = byEntity.World.GetEntityType(assetLocation);
			if (properties is null) {
				byEntity.World.Logger.Error("ItemUndead: No such entity - {0}", assetLocation);
				if (api.World.Side == EnumAppSide.Client) {
					(api as ICoreClientAPI)?.TriggerIngameError(this, "nosuchentity", $"No such entity loaded - '{assetLocation}'.");
				}
				return;
			}
			if (api.World.Side == EnumAppSide.Client) {
				byEntity.World.Logger.Notification("Creating a new entity with code: " + CodeEndWithoutParts(1));
			}
			Entity entity = byEntity.World.ClassRegistry.CreateEntity(properties);
			EntityZombie zombie = entity as EntityZombie;
			ItemStack itemstack = slot.Itemstack;
			JsonObject attributes = Attributes;
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

			// Setup health stuff!
			int durability = GetMaxDurability(itemstack);
			if (durability > 1 && (itemstack.Collectible.GetRemainingDurability(itemstack) * durability) != durability) {
				float health = entity.WatchedAttributes.GetOrAddTreeAttribute("health").GetFloat("basemaxhealth", 20) * GetRemainingDurability(slot.Itemstack);
				entity.WatchedAttributes.GetOrAddTreeAttribute("health").SetFloat("currenthealth", health);
				entity.WatchedAttributes.MarkPathDirty("health");
			}

			// SPAWNING ENTITY //
			byEntity.World.SpawnEntity(entity);

			// Load clothing onto entity!
			for (int i = 0; i < GearsDressCodes.Length; i++) {
				string spawnCode = GearsDressCodes[i] + "Spawn";
				if (properties.Attributes[spawnCode].Exists) {
					try {
						var _items = api.World.GetItem(new AssetLocation(GetRandom(properties.Attributes[spawnCode].AsArray<string>(null))));
						var _stack = new ItemStack(_items, 1);
						_stack.Item.DamageItem(api.World, byEntity, zombie.gearInv[i], _items.Durability / 2);
						if (i == 18 && zombie.gearInv[16].Itemstack.Item is ItemBow) {
							_stack = new ItemStack(_items, GetRandom(_items.MaxStackSize, 5));
						}
						zombie.gearInv[i].Itemstack = _stack;
						zombie.GearInvSlotModified(i);
					} catch { }
				}
			}

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
			ItemStack itemstack = inSlot.Itemstack;
			if (itemstack.Attributes.HasAttribute("appliedNames")) {
				var nametree = itemstack.Attributes.GetTreeAttribute("appliedNames");
				dsc.AppendLine($"{nametree.GetString("name")} {nametree.GetString("last")}");
			}
			int maxDurability = GetMaxDurability(itemstack);
			if (maxDurability > 1) {
				dsc.AppendLine($"<font color=\"#ff8888\">Health: {itemstack.Collectible.GetRemainingDurability(itemstack)}%</font>");
			}
			if (itemstack.Attributes.HasAttribute("entInventory")) {
				var geartree = itemstack.Attributes.GetTreeAttribute("entInventory");
				for (int i = 0; i < GearsDressCodes.Length; i++) {
					string stackCode = GearsDressCodes[i] + "Stack";
					if (geartree.HasAttribute(stackCode)) {
						/** Not important enough to cause a crash or for me to fix. **/
						try { dsc.AppendLine(GetInventoryGear(geartree, stackCode)); } catch { }
					}
				}
			}
		}

		private static string GetInventoryGear(ITreeAttribute inventory, string slotName) {
			if (inventory.HasAttribute(slotName) && inventory.GetItemstack(slotName) != null) {
				var itemstack = inventory.GetItemstack(slotName);
				int durability = itemstack.Attributes.HasAttribute("durability") ? itemstack.Item.GetRemainingDurability(itemstack) : 100;
				int itemAmount = itemstack.StackSize;
				string percentage = ColorTranslator.ToHtml(ColorFromHSV(Math.Clamp((double)durability, 0, 100), 0.4, 1));
				string itemEnding = "";
				if (durability < 100) {
					itemEnding += $" (<font color=\"{percentage}\">{durability}%</font>)";
				}
				if (itemAmount > 1) {
					itemEnding += $" ({itemAmount}x)";
				}
				return $"{Lang.Get(itemstack.Item?.Code?.Domain + ":item-" + itemstack.Item?.Code.Path) ?? "unknown"}{itemEnding}";
			}
			return "null";
		}
	}
}