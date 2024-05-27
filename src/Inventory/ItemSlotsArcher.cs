using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VSKingdom {
	public abstract class ItemSlotsArcher : ItemSlot {
		public ItemSlotsArcher(InventoryArcher inventory) : base(inventory) { }
	}

	public class ItemSlotArcherWear : ItemSlotsArcher {
		protected static readonly string[] iconSlot = new string[] { "gloves", "cape", "shirt", "trousers", "boots", "hat", "necklace", "medal", "mask", "belt", "bracers", "pullover", "armor-helmet", "armor-body", "armor-legs" };
		
		protected EnumCharacterDressType dressType;

		public bool IsArmorSlot { get; protected set; }

		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Outfit;

		public ItemSlotArcherWear(InventoryArcher inventory, int slotId) : base(inventory) {
			dressType = (EnumCharacterDressType)slotId;
			IsArmorSlot = IsArmor((EnumCharacterDressType)slotId);
			BackgroundIcon = iconSlot[slotId];
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

	public class ItemSlotArcherHand : ItemSlotsArcher {
		public ItemSlotArcherHand(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "longbows";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Check if the item is a bow.
			return sourceSlot?.Itemstack?.Item is ItemBow;
		}
	}

	public class ItemSlotArcherLeft : ItemSlotsArcher {
		public ItemSlotArcherLeft(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "offhand";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Check if the item is a block.
			return sourceSlot?.Itemstack?.Block is BlockLantern;
		}
	}

	public class ItemSlotArcherBack : ItemSlotsArcher {
		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Backpack;

		public ItemSlotArcherBack(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "basket";
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

	public class ItemSlotArcherAmmo : ItemSlotsArcher {
		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Arrow;
		public ItemSlotArcherAmmo(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "munition";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Only allow arrows or other types of ammo.
			return sourceSlot?.Itemstack?.Item is ItemArrow || (sourceSlot?.Itemstack?.Collectible?.Attributes["projectile"]?.Exists ?? false);
		}
	}

	public class ItemSlotArcherHeal : ItemSlotsArcher {
		public ItemSlotArcherHeal(InventoryArcher inventory) : base(inventory) {
			BackgroundIcon = "bandages";
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