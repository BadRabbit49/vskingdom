using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VSKingdom {
	public abstract class ItemSlotArcher : ItemSlot {
		public ItemSlotArcher(InventoryArcher inventory) : base(inventory) { }
	}

	public class ItemSlotArcherWear : ItemSlotArcher {
		protected static Dictionary<EnumCharacterDressType, string> iconByDressType = new Dictionary<EnumCharacterDressType, string> {
			{ EnumCharacterDressType.Foot, "boots" },
			{ EnumCharacterDressType.Hand, "gloves" },
			{ EnumCharacterDressType.Shoulder, "cape" },
			{ EnumCharacterDressType.Head, "hat" },
			{ EnumCharacterDressType.LowerBody, "trousers" },
			{ EnumCharacterDressType.UpperBody, "shirt" },
			{ EnumCharacterDressType.UpperBodyOver, "pullover" },
			{ EnumCharacterDressType.Neck, "necklace" },
			{ EnumCharacterDressType.Arm, "bracers" },
			{ EnumCharacterDressType.Waist, "belt" },
			{ EnumCharacterDressType.Emblem, "medal" },
			{ EnumCharacterDressType.Face, "mask" }
		};
		protected EnumCharacterDressType dressType;

		public bool IsArmorSlot { get; protected set; }

		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Outfit;

		public ItemSlotArcherWear(InventoryArcher inventory, EnumCharacterDressType dressType, int slotId) : base(inventory) {
			this.dressType = dressType;
			IsArmorSlot = IsArmor(dressType);
			switch (slotId) {
				case 12: BackgroundIcon = "head"; break;
				case 13: BackgroundIcon = "body"; break;
				case 14: BackgroundIcon = "legs"; break;
				default: iconByDressType.TryGetValue(dressType, out BackgroundIcon); break;
			}
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			return ItemSlotCharacter.IsDressType(sourceSlot?.Itemstack, dressType);
		}

		public static bool IsArmor(EnumCharacterDressType dressType) {
			switch (dressType) {
				case EnumCharacterDressType.ArmorHead:
				case EnumCharacterDressType.ArmorBody:
				case EnumCharacterDressType.ArmorLegs:
				return true;
			}
			return false;
		}
	}

	public class ItemSlotArcherHand : ItemSlotArcher {
		public ItemSlotArcherHand(InventoryArcher inventory, int slotId) : base(inventory) {
			switch (slotId) {
				case 15: BackgroundIcon = "shield"; StorageType = EnumItemStorageFlags.Offhand; break;
				case 16: BackgroundIcon = "sword"; break;
			}
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Check if the item can be placed on a toolrack, or held like a lantern or torch.
			return sourceSlot?.Itemstack?.Collectible?.Attributes?["toolrackTransform"]?.Exists ?? sourceSlot?.Itemstack?.Collectible?.Attributes?["heldTpIdleAnimation"]?.Exists ?? false;
		}
	}

	public class ItemSlotArcherBack : ItemSlotArcher {
		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Backpack;

		public ItemSlotArcherBack(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "backpack";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Only allow empty backpacks for now to avoid issues.
			return CollectibleObject.IsEmptyBackPack(sourceSlot.Itemstack);
		}
	}

	public class ItemSlotArcherAmmo : ItemSlotArcher {
		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Arrow;
		public ItemSlotArcherAmmo(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "vskingdom:textures/icons/quiver";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Only allow arrows or other types of ammo.
			return sourceSlot.Itemstack?.Item is ItemArrow || (sourceSlot?.Itemstack?.Collectible?.Attributes["projectile"]?.Exists ?? false);
		}
	}

	public class ItemSlotArcherHeal : ItemSlotArcher {
		public ItemSlotArcherHeal(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "itemslot-bandage";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Make sure the item is a healing item.
			return sourceSlot?.Itemstack?.Item?.Code?.Path?.Contains("bandage") ?? false;
		}
	}
}