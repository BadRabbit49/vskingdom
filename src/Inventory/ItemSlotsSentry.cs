using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VSKingdom;

public abstract class ItemSlotsSentry : ItemSlot {
	public ItemSlotsSentry(InventorySentry inventory) : base(inventory) { }
}

public class ItemSlotKnightWear : ItemSlotsSentry {
	public ItemSlotKnightWear(InventorySentry inventory, int slotId) : base(inventory) {
		dressType = (EnumCharacterDressType)slotId;
		IsArmorSlot = IsArmor((EnumCharacterDressType)slotId);
		BackgroundIcon = iconSlot[slotId];
	}

	public static readonly string[] iconSlot = new string[] { "gloves", "cape", "shirt", "trousers", "boots", "hat", "necklace", "medal", "mask", "belt", "bracers", "pullover", "armor-helmet", "armor-body", "armor-legs" };

	public bool IsArmorSlot { get; private set; }
	
	public EnumCharacterDressType dressType { get; private set; }

	public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Outfit;

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

public class ItemSlotKnightHand : ItemSlotsSentry {
	public ItemSlotKnightHand(InventorySentry inventory) : base(inventory) {
		BackgroundIcon = "weaponry";
	}

	public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
		return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
	}

	public override bool CanHold(ItemSlot sourceSlot) {
		return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
	}

	private bool IsAcceptable(ItemSlot sourceSlot) {
		// Check if the item can be placed on a toolrack, used as a bow or any kind of appropriate weapon.
		return sourceSlot?.Itemstack?.Collectible?.Attributes?["toolrackTransform"]?.Exists ?? false;
	}
}

public class ItemSlotKnightLeft : ItemSlotsSentry {
	public ItemSlotKnightLeft(InventorySentry inventory) : base(inventory) {
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

public class ItemSlotKnightBack : ItemSlotsSentry {
	public ItemSlotKnightBack(InventorySentry inventory) : base(inventory) {
		BackgroundIcon = "backpack";
	}

	public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Backpack;

	public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
		return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
	}

	public override bool CanHold(ItemSlot sourceSlot) {
		return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
	}

	private bool IsAcceptable(ItemSlot sourceSlot) {
		// If correct class, only allow empty backpacks for now to avoid issues.
		return CollectibleObject.IsEmptyBackPack(sourceSlot.Itemstack);
	}
}

public class ItemSlotKnightAmmo : ItemSlotsSentry {
	public ItemSlotKnightAmmo(InventorySentry inventory) : base(inventory) {
		BackgroundIcon = "munition";
	}

	public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
		return IsAcceptable(sourceSlot) && base.CanTakeFrom(sourceSlot, priority);
	}

	public override bool CanHold(ItemSlot sourceSlot) {
		return IsAcceptable(sourceSlot) && base.CanHold(sourceSlot);
	}

	private bool IsAcceptable(ItemSlot sourceSlot) {
		return sourceSlot.Itemstack.Class == EnumItemClass.Item && (sourceSlot?.Itemstack?.Item is ItemArrow || sourceSlot?.Itemstack?.Item is ItemStone || sourceSlot.Itemstack.Item.Attributes["projectile"].Exists);
	}
}

public class ItemSlotKnightHeal : ItemSlotsSentry {
	public ItemSlotKnightHeal(InventorySentry inventory) : base(inventory) {
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