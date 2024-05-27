using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VSKingdom {
	public abstract class ItemSlotsKnight : ItemSlot {
		public ItemSlotsKnight(InventoryKnight inventory) : base(inventory) { }
	}

	public class ItemSlotKnightWear : ItemSlotsKnight {
		protected static readonly string[] iconSlot = new string[] { "gloves", "cape", "shirt", "trousers", "boots", "hat", "necklace", "medal", "mask", "belt", "bracers", "pullover", "armor-helmet", "armor-body", "armor-legs" };
		
		protected EnumCharacterDressType dressType;

		public bool IsArmorSlot { get; protected set; }

		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Outfit;

		public ItemSlotKnightWear(InventoryKnight inventory, int slotId) : base(inventory) {
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

	public class ItemSlotKnightHand : ItemSlotsKnight {
		public ItemSlotKnightHand(InventoryKnight inventory) : base(inventory) {
			BackgroundIcon = "weaponry";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Check if the item can be placed on a toolrack, or held like a lantern or torch.
			return sourceSlot?.Itemstack?.Collectible?.Attributes?["toolrackTransform"]?.Exists ?? false;
		}
	}

	public class ItemSlotKnightLeft : ItemSlotsKnight {
		public ItemSlotKnightLeft(InventoryKnight inventory) : base(inventory) {
			BackgroundIcon = "shieldry";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Check if the item is a bow.
			return sourceSlot?.Itemstack?.Block is BlockLantern || sourceSlot?.Itemstack?.Item is ItemShield;
		}
	}

	public class ItemSlotKnightBack : ItemSlotsKnight {
		public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Backpack;

		public ItemSlotKnightBack(InventoryKnight inventory) : base(inventory) {
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

	public class ItemSlotKnightFood : ItemSlotsKnight {
		public ItemSlotKnightFood(InventoryKnight inventory) : base(inventory) {
			BackgroundIcon = "baguette";
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
			return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
		}

		public override bool CanHold(ItemSlot sourceSlot) {
			return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
		}

		private bool IsAcceptable(ItemSlot sourceSlot) {
			// Make sure the item is a healing item.
			return sourceSlot?.Itemstack?.Item?.Code?.Path?.Contains("bread") ?? false;
		}
	}

	public class ItemSlotKnightHeal : ItemSlotsKnight {
		public ItemSlotKnightHeal(InventoryKnight inventory) : base(inventory) {
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