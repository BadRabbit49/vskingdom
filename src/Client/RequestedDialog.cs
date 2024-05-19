using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSKingdom {
	public class RequestedDialog : GuiDialog {
		protected IServerPlayer mailedPlayer;
		protected IServerPlayer senderPlayer;
		protected string senderKingdom;
		protected string messagedTypes;
		public override bool DisableMouseGrab => false;
		public override bool PrefersUngrabbedMouse => false;
		public override bool UnregisterOnClose => true;
		public override double DrawOrder => 0.2;
		public override string ToggleKeyCombinationCode => null;

		public RequestedDialog(IServerPlayer mailedPlayer, IServerPlayer senderPlayer, string senderKingdom, string messagedType, ICoreClientAPI capi) : base(capi) {
			this.mailedPlayer = mailedPlayer;
			this.senderPlayer = senderPlayer;
			this.senderKingdom = senderKingdom;
			this.messagedTypes = messagedType;

			ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
			bgBounds.BothSizing = ElementSizing.FitToChildren;
			
			ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0.0);

			ElementBounds messageBounds = ElementBounds.Fixed(0.0, 0.0, 100, 50).FixedGrow(1.0, 2.0);

			ElementBounds AcceptButtonBounds = ElementStdBounds.ToggleButton(0.0, 22.0, 100, 50).FixedGrow(1.0, 2.0);
			ElementBounds RejectButtonBounds = ElementStdBounds.ToggleButton(0.0, 22.0, 100, 50).FixedGrow(1.0, 2.0);

			RejectButtonBounds.FixedRightOf(AcceptButtonBounds, 10.0);

			SingleComposer = capi.Gui.CreateCompo("requestscreen" + mailedPlayer.PlayerUID + messagedType + senderPlayer.PlayerUID, dialogBounds)
				.AddShadedDialogBG(bgBounds)
				.AddDialogTitleBar(messagedType, onClose: OnCloseDialog);
			SingleComposer
				.GetDynamicText("messagedText")
				.SetNewText(GetMessage());
			SingleComposer
				.AddButton(GetOptions(true), () => OnAccept(), AcceptButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "acceptResponse")
				.AddButton(GetOptions(false), () => OnReject(), RejectButtonBounds, CairoFont.WhiteSmallishText(), EnumButtonStyle.Normal, "rejectResponse")
			.EndChildElements();
			SingleComposer.Compose();
		}

		public override void OnGuiOpened() {
			base.OnGuiOpened();
		}

		public override void OnGuiClosed() {
			base.OnGuiClosed();
		}

		public void JoinKingdom() {
			if (mailedPlayer.Entity.HasBehavior<EntityBehaviorLoyalties>() && senderKingdom != null) {
				mailedPlayer.Entity.GetBehavior<EntityBehaviorLoyalties>()?.SetKingdom(senderKingdom);
			}
		}

		public string GetOptions(bool button) {
			switch (messagedTypes) {
				case "invite":
					if (button) {
						return LangUtility.Get("command-choice-accepts");
					} else {
						return LangUtility.Get("command-choice-rejects");
					}
				case "become":
					if (button) {
						return LangUtility.Get("command-choice-accepts");
					} else {
						return LangUtility.Get("command-choice-rejects");
					}
				case "voting":
					if (button) {
						return LangUtility.Get("command-choice-voteyay");
					} else {
						return LangUtility.Get("command-choice-votenay");
					}
				case "rebels":
					if (button) {
						return LangUtility.Get("command-choice-joinreb");
					} else {
						return LangUtility.Get("command-choice-stayloy");
					}
				default:
					if (button) {
						return LangUtility.Get("command-choice-accepts");
					} else {
						return LangUtility.Get("command-choice-rejects");
					}
			}
		}

		public string GetMessage() {
			switch (messagedTypes) {
				case "invite":
					return (senderPlayer.PlayerName + LangUtility.Get("command-choice-message-invite0") + DataUtility.GetKingdomName(senderKingdom) + LangUtility.Get("command-choice-message-invite1"));
				case "become":
					return (senderPlayer.PlayerName + LangUtility.Get("command-choice-message-become0") + DataUtility.GetKingdomName(senderKingdom) + LangUtility.Get("command-choice-message-become1"));
				case "voting":
					return (senderPlayer.PlayerName + LangUtility.Get("command-choice-message-voting0") + DataUtility.GetKingdomName(senderKingdom) + LangUtility.Get("command-choice-message-voting1"));
				case "rebels":
					return (senderPlayer.PlayerName + LangUtility.Get("command-choice-message-rebels0") + DataUtility.GetKingdomName(senderKingdom) + LangUtility.Get("command-choice-message-rebels1"));
				default:
					return null;
			}
		}

		protected void OnCloseDialog() {
			TryClose();
		}

		protected bool OnAccept() {
			// Activate response protocals for positive.
			switch (messagedTypes) {
				case "invite":
					JoinKingdom();
					break;
				case "become":
					JoinKingdom();
					break;
				case "voting":
					// TO BE IMPLEMENTED.
					break;
				case "rebels":
					// TO BE IMPLEMENTED.
					break;
			}
			TryClose();
			return true;
		}

		protected bool OnReject() {
			// Activate response protocals for negative.
			switch (messagedTypes) {
				case "invite":
					break;
				case "become":
					break;
				case "voting":
					// TO BE IMPLEMENTED.
					break;
				case "rebels":
					// TO BE IMPLEMENTED.
					break;
			}
			TryClose();
			return true;
		}
	}
}
