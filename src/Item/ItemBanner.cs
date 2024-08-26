using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class ItemBanner : Item {
		public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity) {
			base.OnHeldIdle(slot, byEntity);
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
			base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
			if (byEntity is EntityPlayer player && player.Api.Side == EnumAppSide.Client) {
				Vec3d coordinates = (player.Player.CurrentBlockSelection?.FullPosition ?? player.Player.CurrentEntitySelection?.Entity?.ServerPos.AsBlockPos.ToVec3d() ?? player.ServerPos.AsBlockPos.ToVec3d());
				(player.Api as ICoreClientAPI)?.ShowChatMessage($"[{coordinates.X},{coordinates.Y},{coordinates.Z}]");
			}
		}

		public override void OnHeldActionAnimStart(ItemSlot slot, EntityAgent byEntity, EnumHandInteract type) {
			base.OnHeldActionAnimStart(slot, byEntity, type);
			slot.Itemstack.Attributes.SetInt("renderVariant", 1);
		}

		public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand) {
			return base.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand);
		}
	}
}