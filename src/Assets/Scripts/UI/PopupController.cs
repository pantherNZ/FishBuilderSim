using System;
using System.Collections.Generic;
using Runtime.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
	public class Popup
	{
		public enum ReturnState
		{
			Yes,
			No,
			Cancel
		}

		public enum CloseMode
		{
			InnerWorkflow,
			InnerWorkflowOrOutterClick,
			InnerWorkflowOrAnyClick
		}

		public static void Prompt(VisualElement root, string text)
		{
			var label = new Label();
			label.text = text;
			label.styleSheets.Add(Resources.Load<StyleSheet>("UI/USS/Generic"));
			label.AddToClassList("MainWindow");
			label.AddToClassList("PopupMinSize");
			label.AddToClassList("CenteredLabel");
			Custom(root, label, CloseMode.InnerWorkflowOrAnyClick);
		}

		public static void Custom(VisualElement root, VisualElement visualElement, CloseMode closeMode = CloseMode.InnerWorkflowOrOutterClick, Action callback = null, bool playAudio = true)
		{
			var popupController = new PopupController(visualElement, closeMode, callback, playAudio);
			var head = Utility.UI.Head(root);
			popupController.root = head;
			head.Add(popupController.selfView);
		}

		public static void YesNoCancel(VisualElement root, Schema.PopupSchema schema, Action<ReturnState> callback = null, bool playAudio = true)
		{
			var popupTemplate = Runtime.Game.GlobalConstantsHandler.UIConstants.yesNoPopupTemplate;
			var visual = popupTemplate.Instantiate();
			new YesNoCancelPopupController(visual, schema, callback, playAudio);
			Custom(root, visual, playAudio: playAudio);
		}

		private Popup() { }

		public class InnerController
		{
			public Action exitPopup;

			public virtual void OnExitClicked()
			{
				exitPopup?.Invoke();
			}
		}
	}

	internal class PopupController
	{
		internal static List<PopupController> activePopups = new List<PopupController>();
		internal VisualElement root;
		internal VisualElement selfView;
		VisualElement childView;
		internal VisualElement exit;
		bool playAudio = true;
		Action callback;

		internal PopupController(VisualElement childView, Popup.CloseMode closeMode, Action callback = null, bool playAudio = true)
		{
			this.childView = childView;
			this.playAudio = playAudio;
			this.callback = callback;
			var popupTemplate = Runtime.Game.GlobalConstantsHandler.UIConstants.genericPopupTemplate;
			selfView = popupTemplate.Instantiate();
			selfView.style.position = Position.Absolute;
			selfView.style.left = 0;
			selfView.style.right = 0;
			selfView.style.top = 0;
			selfView.style.bottom = 0;
			selfView.focusable = true;
			selfView.schedule.Execute(selfView.Focus).StartingIn(100);
			selfView.RegisterCallback<KeyUpEvent>(OnKeyUpEvent);
			exit = selfView.Q<VisualElement>("Exit");
			exit.Add(childView);
			var closePopupTemplate = Runtime.Game.GlobalConstantsHandler.UIConstants.closePopupTemplate;
			var closeView = closePopupTemplate.Instantiate();
			closeView.style.position = Position.Absolute;
			closeView.style.right = 0;
			closeView.style.top = 0;
			childView.Add(closeView);
			var closeButton = closeView.Q<Button>("Close");
			if (closeMode != Popup.CloseMode.InnerWorkflowOrAnyClick)
				childView.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
			if (childView.userData is Popup.InnerController innerController)
			{
				innerController.exitPopup = ExitPopup;
				closeButton.clicked += () => innerController.OnExitClicked();
				if (closeMode != Popup.CloseMode.InnerWorkflow)
					exit.RegisterCallback<ClickEvent>(evt => innerController.OnExitClicked());
			}
			else
			{
				closeButton.clicked += ExitPopup;
				if (closeMode != Popup.CloseMode.InnerWorkflow)
					exit.RegisterCallback<ClickEvent>(evt => ExitPopup());
			}

			activePopups.Add(this);
		}

		public virtual void ExitPopup()
		{
			activePopups.Remove(this);
			if (activePopups.Count > 0)
			{
				var newTopView = activePopups.Back().selfView;
				newTopView.schedule.Execute(newTopView.Focus).StartingIn(100);
			}
			root.Remove(selfView);

			if (playAudio)
				SfxManager.Instance.PlayUI(Schema.AudioType.ButtonPressGenericClose);

			callback?.Invoke();
		}

		void OnKeyUpEvent(KeyUpEvent evt)
		{
			if (evt.keyCode != KeyCode.Escape)
				return;

			if (childView.userData is Popup.InnerController innerController)
				innerController.OnExitClicked();
			else
				ExitPopup();
		}
	}

	internal class YesNoCancelPopupController : Popup.InnerController
	{
		internal static List<YesNoCancelPopupController> activePopups = new List<YesNoCancelPopupController>();
		readonly Action<Popup.ReturnState> callback;
		bool playAudio = true;

		internal YesNoCancelPopupController(VisualElement root, Schema.PopupSchema schema, Action<Popup.ReturnState> callback, bool playAudio = true)
		{
			this.playAudio = playAudio;
			root.userData = this;
			root.Q<Label>("Header").text = schema.header;
			root.Q<Label>("Prompt").text = schema.body;
			root.Q<Button>("Close").clicked += () => OnClicked(Popup.ReturnState.Cancel);
			SetButton(root.Q<Button>("Yes"), schema.yes, Popup.ReturnState.Yes);
			SetButton(root.Q<Button>("No"), schema.no, Popup.ReturnState.No);
			SetButton(root.Q<Button>("Cancel"), schema.cancel, Popup.ReturnState.Cancel);
			root.Q<VisualElement>("Image").style.backgroundImage = new StyleBackground(schema.image);
			this.callback = callback;
			activePopups.Add(this);
		}

		void SetButton(Button button, string label, Popup.ReturnState returnState)
		{
			if (label.IsEmpty())
			{
				button.style.display = DisplayStyle.None;
				return;
			}
			button.text = label;
			button.clicked += () => OnClicked(returnState);
		}

		void OnClicked(Popup.ReturnState state)
		{
			activePopups.Remove(this);
			exitPopup();
			callback?.Invoke(state);

			if (playAudio)
				SfxManager.Instance.PlayUI(state == Popup.ReturnState.Yes ? Schema.AudioType.ButtonPressGenericOpen : Schema.AudioType.ButtonPressGenericClose);
		}

		public override void OnExitClicked()
		{
			OnClicked(Popup.ReturnState.Cancel);
		}
	}
}
