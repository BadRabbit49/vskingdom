using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class InventoryDialog : GuiDialog {
		protected Vec3d entityPos = new Vec3d();
		protected InventoryArcher inv;
		protected EntityArcher ent;
		protected bool IsInRangeOfEntity => capi.World.Player.Entity.Pos.XYZ.Add(capi.World.Player.Entity.LocalEyePos).SquareDistanceTo(entityPos) <= Math.Pow(capi.World.Player.WorldData.PickingRange, 2);
		protected double FloatyDialogPosition => 0.6;
		protected double FloatyDialogAlign => 0.8;

		public override bool DisableMouseGrab => false;
		public override bool PrefersUngrabbedMouse => false;
		public override bool UnregisterOnClose => true;
		public override double DrawOrder => 0.2;
		public override string ToggleKeyCombinationCode => null;

		private static readonly double slotPad = GuiElementItemSlotGridBase.unscaledSlotPadding;

		public InventoryDialog(InventoryArcher inv, EntityArcher ent, ICoreClientAPI capi) : base(capi) {
			this.inv = inv;
			this.ent = ent;

			ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
			bgBounds.BothSizing = ElementSizing.FitToChildren;

			ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0.0);

			ElementBounds armourSlotBoundsHead = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + slotPad, 1, 1).FixedGrow(0.0, slotPad);
			ElementBounds armourSlotBoundsBody = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 122.0 + slotPad, 1, 1).FixedGrow(0.0, slotPad);
			ElementBounds armourSlotBoundsLegs = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 224.0 + slotPad, 1, 1).FixedGrow(0.0, slotPad);

			ElementBounds clothingsSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + slotPad, 1, 6).FixedGrow(0.0, slotPad);
			ElementBounds accessorySlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + slotPad, 1, 6).FixedGrow(0.0, slotPad);

			ElementBounds rightSlotBoundsLHand = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 020.0 + slotPad, 1, 1).FixedGrow(0.0, slotPad);
			ElementBounds rightSlotBoundsRHand = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 071.0 + slotPad, 1, 1).FixedGrow(0.0, slotPad);
			ElementBounds backpackOnSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 122.0 + slotPad, 1, 1).FixedGrow(0.0, slotPad);

			ElementBounds munitionsSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 173.0 + slotPad, 1, 6).FixedGrow(0.0, slotPad);
			ElementBounds healthitmSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 224.0 + slotPad, 1, 6).FixedGrow(0.0, slotPad);

			ElementBounds goWanderButtonBounds = ElementStdBounds.ToggleButton(0.0, 326.0 + slotPad, 102, 51).FixedGrow(0.0, slotPad);
			ElementBounds followMeButtonBounds = ElementStdBounds.ToggleButton(0.0, 326.0 + slotPad, 102, 51).FixedGrow(0.0, slotPad);
			ElementBounds stayHereButtonBounds = ElementStdBounds.ToggleButton(0.0, 377.0 + slotPad, 102, 51).FixedGrow(0.0, slotPad);
			ElementBounds goReturnButtonBounds = ElementStdBounds.ToggleButton(0.0, 377.0 + slotPad, 102, 51).FixedGrow(0.0, slotPad);

			ElementBounds switchesButtonBounds = ElementStdBounds.ToggleButton(0.0, 428.0 + slotPad, 214, 51).FixedGrow(0.0, slotPad);

			clothingsSlotsBounds.FixedRightOf(armourSlotBoundsHead, 10.0).FixedRightOf(armourSlotBoundsBody, 10.0).FixedRightOf(armourSlotBoundsLegs, 10.0);
			accessorySlotsBounds.FixedRightOf(clothingsSlotsBounds, 10.0);
			clothingsSlotsBounds.fixedHeight -= 6.0;
			accessorySlotsBounds.fixedHeight -= 6.0;
			backpackOnSlotBounds.FixedRightOf(accessorySlotsBounds, 10.0);
			rightSlotBoundsLHand.FixedRightOf(accessorySlotsBounds, 10.0);
			rightSlotBoundsRHand.FixedRightOf(accessorySlotsBounds, 10.0);
			munitionsSlotsBounds.FixedRightOf(accessorySlotsBounds, 10.0);
			healthitmSlotsBounds.FixedRightOf(accessorySlotsBounds, 10.0);

			followMeButtonBounds.FixedRightOf(goWanderButtonBounds, 10.0);
			goReturnButtonBounds.FixedRightOf(stayHereButtonBounds, 10.0);

			SingleComposer = capi.Gui.CreateCompo("soldiercontents" + ent.EntityId, dialogBounds)
				.AddShadedDialogBG(bgBounds)
				.AddDialogTitleBar(GetName(ent), onClose: OnCloseDialog);
			SingleComposer
				.BeginChildElements(bgBounds);
			SingleComposer
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.HeadArmorSlotId }, armourSlotBoundsHead, "armorSlotsHead")
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.BodyArmorSlotId }, armourSlotBoundsBody, "armorSlotsBody")
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.LegsArmorSlotId }, armourSlotBoundsLegs, "armorSlotsLegs");
			SingleComposer
				.AddItemSlotGrid(inv, SendInvPacket, 1, InventoryArcher.ClothingsSlotIds, clothingsSlotsBounds, "clothingsSlots")
				.AddItemSlotGrid(inv, SendInvPacket, 1, InventoryArcher.AccessorySlotIds, accessorySlotsBounds, "accessorySlots");
			SingleComposer
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.LHandItemSlotId }, rightSlotBoundsLHand, "otherSlotsLHnd")
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.RHandItemSlotId }, rightSlotBoundsRHand, "otherSlotsRHnd")
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.BPackItemSlotId }, backpackOnSlotBounds, "otherSlotsPack")
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.MunitionsSlotId }, munitionsSlotsBounds, "otherSlotsAmmo")
				.AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { InventoryArcher.HealthItmSlotId }, healthitmSlotsBounds, "otherSlotsHeal");
			SingleComposer
				.AddButton(LangUtility.Get("gui-profile-wander"), () => OnClick(CurrentCommand.WANDER), goWanderButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersGoWander")
				.AddButton(LangUtility.Get("gui-profile-follow"), () => OnClick(CurrentCommand.FOLLOW), followMeButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersFollowMe")
				.AddButton(LangUtility.Get("gui-profile-nomove"), () => OnClick(CurrentCommand.NOMOVE), stayHereButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersDontMove")
				.AddButton(LangUtility.Get("gui-profile-return"), () => OnClick(CurrentCommand.RETURN), goReturnButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersGoReturn")
				.AddButton(LangUtility.Get("gui-profile-switch"), () => OnClass(), switchesButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "commandReClass")
			.EndChildElements();
			SingleComposer.Compose();
		}

		public override void OnFinalizeFrame(float dt) {
			base.OnFinalizeFrame(dt);
			entityPos = ent.Pos.XYZ.Clone();
			entityPos.Add(ent.SelectionBox.X2 - ent.OriginSelectionBox.X2, 0.0, ent.SelectionBox.Z2 - ent.OriginSelectionBox.Z2);
			if (!IsInRangeOfEntity) {
				capi.Event.EnqueueMainThreadTask(delegate {
					TryClose();
				}, "closedsoldierinvlog");
			}
		}

		public override void OnGuiClosed() {
			base.OnGuiClosed();
			capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(inv));
			SingleComposer.GetSlotGrid("armorSlotsHead")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("armorSlotsBody")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("armorSlotsLegs")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("clothingsSlots")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("accessorySlots")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsLHnd")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsRHnd")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsPack")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsAmmo")?.OnGuiClosed(capi);
			SingleComposer.GetSlotGrid("otherSlotsHeal")?.OnGuiClosed(capi);
		}

		public override void OnRenderGUI(float deltaTime) {
			if (capi.Settings.Bool["immersiveMouseMode"]) {
				double offX = ent.SelectionBox.X2 - ent.OriginSelectionBox.X2;
				double offZ = ent.SelectionBox.Z2 - ent.OriginSelectionBox.Z2;
				Vec3d pos = MatrixToolsd.Project(new Vec3d(ent.Pos.X + offX, ent.Pos.Y + FloatyDialogPosition, ent.Pos.Z + offZ), capi.Render.PerspectiveProjectionMat, capi.Render.PerspectiveViewMat, capi.Render.FrameWidth, capi.Render.FrameHeight);
				if (pos.Z < 0.0) {
					return;
				}
				SingleComposer.Bounds.Alignment = EnumDialogArea.None;
				SingleComposer.Bounds.fixedOffsetX = 0.0;
				SingleComposer.Bounds.fixedOffsetY = 0.0;
				SingleComposer.Bounds.absFixedX = pos.X - SingleComposer.Bounds.OuterWidth / 2.0;
				SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - pos.Y - SingleComposer.Bounds.OuterHeight * FloatyDialogAlign;
				SingleComposer.Bounds.absMarginX = 0.0;
				SingleComposer.Bounds.absMarginY = 0.0;
			}
			base.OnRenderGUI(deltaTime);
		}

		protected void OnCloseDialog() {
			TryClose();
		}

		protected void SendInvPacket(object packet) {
			capi.Network.SendPacketClient(packet);
		}

		protected bool OnClick(CurrentCommand orders) {
			// Update movement orders here!
			ent.GetBehavior<EntityBehaviorLoyalties>()?.SetUnitOrders(orders);
			TryClose();
			return true;
		}

		protected bool OnClass() {
			/**string fullcode = ent.CodeWithoutParts(2);
			string weapon = ent.Code.Path.Substring(ent.Code.Path.LastIndexOf('-'));
			string gender = ent.Code.EndVariant();
			
			// Spawn an entirely new variant entity with all the same stats, properties, behaviors, etc.
			if (weapon == "melee-") {
				fullcode += "-range-" + gender;
			} else {
				fullcode += "-melee-" + gender;
			}

			EntityProperties entityProperties = ent.World.GetEntityType(new AssetLocation(ent.Code.Domain, fullcode));
			Entity entity = ent.World.ClassRegistry.CreateEntity(entityProperties);

			entity.ServerPos = ent.ServerPos.Copy();
			entity.Pos.SetFrom(ent.ServerPos.Copy());
			entity.PositionBeforeFalling.Set(ent.ServerPos.X, ent.ServerPos.Y, ent.ServerPos.Z);
			entity.Attributes.SetString("origin", "playerplaced");
			
			entity.WatchedAttributes.SetLong("guardedEntityId", ent.WatchedAttributes.GetLong("guardedEntityId"));
			entity.WatchedAttributes.SetString("guardedPlayerUid", ent.WatchedAttributes.GetString("guardedPlayerUid"));
			entity.WatchedAttributes.SetAttribute("nametag", ent.WatchedAttributes.GetTreeAttribute("nametag").Clone());
			entity.WatchedAttributes.SetAttribute("loyalties", ent.WatchedAttributes.GetTreeAttribute("loyalties").Clone());
			entity.WatchedAttributes.SetAttribute("inventory", ent.WatchedAttributes.GetTreeAttribute("inventory").Clone());
			entity.WatchedAttributes.SetAttribute("health", ent.WatchedAttributes.GetTreeAttribute("health").Clone());
			entity.WatchedAttributes.SetAttribute("skinConfig", ent.WatchedAttributes.GetTreeAttribute("skinConfig").Clone());

			ent.World.Logger.Notification("Creating a new entity with code: " + fullcode
			+ "\nNametag: " + entity.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name")
			+ "\nLoyaltiesKINGDOM: " + entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID")
			+ "\nLoyaltiesLEADERS: " + entity.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("leadersUID")
			+ "\nHealthbarHEALTHS: " + entity.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("currenthealth"));
			
			ent.World.Logger.Notification("Entity " + ent.GetName() + " has a kingdom? " + (ent.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") is not null));
			if (ent.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") is not null) {
				ent.World.Logger.Notification("Entity " + ent.GetName() + " belonging to kingdom " + ent.WatchedAttributes.GetTreeAttribute("loyalties")?.GetString("kingdomUID") + " is being swapped.");
				// Getting all the server data to save to. Possibly dangerous? Maybe.
				long oldEntUID = ent.EntityId;
				long newEntUID = entity.EntityId;
				byte[] kingdomData = VSKingdom.serverAPI?.WorldManager.SaveGame.GetData("kingdomData");
				List<Kingdom> kingdomList = kingdomData is null ? new List<Kingdom>() : SerializerUtil.Deserialize<List<Kingdom>>(kingdomData);
				kingdomList.Find(kingdomMatch => kingdomMatch.KingdomUID == ent.GetBehavior<EntityBehaviorLoyalties>()?.kingdomUID).SwitchMember(oldEntUID, newEntUID, kingdomList);
			}

			// Kill this entity remove the body quickly.
			// ent.Die(EnumDespawnReason.Removed);

			// Spawn our new identical entity.
			(VSKingdom.serverAPI.World as ServerMain).SpawnEntity(entity);**/

			TryClose();
			return true;
		}

		public string GetName(EntityArcher ent) {
			// Get the name of the soldier.
			if (ent.HasBehavior<EntityBehaviorNameTag>()) {
				return ent.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
			} else {
				string name = ent.GetName();
				return name.Length > 30 ? name.Substring(0, 30) : name;
			}
		}
	}
}