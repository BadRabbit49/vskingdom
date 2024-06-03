using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class BlockEntityBody : BlockEntityContainer {
		public virtual InventorySentry gearInv { get; set; }
		public override string InventoryClassName => "gear-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z;
		public override InventoryBase Inventory => gearInv;
		
		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			// Initialize gear slots if not done yet.
			if (gearInv is null) {
				gearInv = new InventorySentry(InventoryClassName, api);
			} else {
				Inventory.LateInitialize(InventoryClassName, api);
			}
		}
		
		public override void OnBlockBroken(IPlayer byPlayer = null) {
			base.OnBlockBroken(byPlayer);
			if (Api.World is IServerWorldAccessor) {
				Inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
			}
		}
		
	}
}