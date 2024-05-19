using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VSKingdom {
	public class BlockEntityBody : BlockEntityContainer {
		public InventoryArcher gearInv { get; set; }
		public override InventoryBase Inventory => gearInv;
		public override string InventoryClassName => "gear-" + +Pos.X + "/" + Pos.Y + "/" + Pos.Z;
		
		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			// Initialize gear slots if not done yet.
			if (gearInv is null) {
				gearInv = new InventoryArcher(InventoryClassName, api);
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