using Vintagestory.API.Common;

namespace VSKingdom {
	public class InventoryKnight : InventoryGeneric {
		public InventoryKnight(string className, string instanceId, ICoreAPI api) : base(20, className, instanceId, api) { }
		public InventoryKnight(string invId, ICoreAPI api) : base(20, invId, api) { }

		public static readonly int[] ClothingsSlotIds = new int[6] { 0, 1, 2, 11, 3, 4 };
		public static readonly int[] AccessorySlotIds = new int[6] { 6, 7, 8, 10, 5, 9 };
		public const int HeadArmorSlotId = 12;
		public const int BodyArmorSlotId = 13;
		public const int LegsArmorSlotId = 14;
		public const int LHandItemSlotId = 15;
		public const int RHandItemSlotId = 16;
		public const int BPackItemSlotId = 17;
		public const int FoodsItemSlotId = 18;
		public const int HealthItmSlotId = 19;
		public ItemSlot LeftHandSlot { get; set; }
		public ItemSlot RightHandSlot { get; set; }
		public ItemSlot BackItemSlot => this[17];
		public ItemSlot FoodItemSlot => this[18];
		public ItemSlot HealItemSlot => this[19];

		public override ItemSlot this [int slotId] { get => slots[slotId]; set => slots[slotId] = value; }

		public override int Count => slots.Length;
		
		public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge) {
			return (!isMerge) ? (baseWeight + 1f) : (baseWeight + 3f);
		}

		protected override ItemSlot NewSlot(int slotId) {
			switch (slotId) {
				case 15: return new ItemSlotKnightLeft(this);
				case 16: return new ItemSlotKnightHand(this);
				case 17: return new ItemSlotKnightBack(this);
				case 18: return new ItemSlotKnightFood(this);
				case 19: return new ItemSlotKnightHeal(this);
				default: return new ItemSlotKnightWear(this, slotId);
			}
		}
	}
}