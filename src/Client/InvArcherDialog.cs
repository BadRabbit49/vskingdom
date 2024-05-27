using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSKingdom {
	public class InvArcherDialog : GuiDialog {
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

		public InvArcherDialog(InventoryArcher inv, EntityArcher ent, ICoreClientAPI capi) : base(capi) {
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
				.AddButton(LangUtility.Get("gui-profile-wander"), () => OnClick(new bool[] { true, false, false, false }), goWanderButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersGoWander")
				.AddButton(LangUtility.Get("gui-profile-follow"), () => OnClick(new bool[] { false, true, false, false }), followMeButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersFollowMe")
				.AddButton(LangUtility.Get("gui-profile-nomove"), () => OnClick(new bool[] { false, false, true, false }), stayHereButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersDontMove")
				.AddButton(LangUtility.Get("gui-profile-return"), () => OnClick(new bool[] { false, false, false, true }), goReturnButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "ordersGoReturn")
			.EndChildElements();
			SingleComposer.Compose();
		}

		public override void OnFinalizeFrame(float dt) {
			base.OnFinalizeFrame(dt);
			entityPos = ent.Pos.XYZ.Clone();
			entityPos.Add(ent.SelectionBox.X2 - ent.OriginSelectionBox.X2, 0.0, ent.SelectionBox.Z2 - ent.OriginSelectionBox.Z2);
			if (!IsInRangeOfEntity) {
				capi.Event.EnqueueMainThreadTask(delegate { TryClose(); }, "closedarcherinvlog");
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

		protected bool OnClick(bool[] orders) {
			// Update movement orders here!
			ent.GetBehavior<EntityBehaviorLoyalties>()?.SetUnitOrders(orders);
			TryClose();
			return true;
		}

		protected bool OnClass() {
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