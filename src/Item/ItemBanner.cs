using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSKingdom {
	public class ItemBanner : Item {
		public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand) {
			return null;
		}
	}
}