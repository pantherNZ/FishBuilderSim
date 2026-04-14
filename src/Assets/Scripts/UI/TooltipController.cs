using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
	class TooltipController
	{
		VisualElement head;
		VisualElement root;
		VisualElement trigger;
		VisualElement inners;
		IVisualElementScheduledItem update;
		Rect triggerBound;
		bool topBottom;

		static public void Bind(VisualElement trigger, Func<VisualElement> innersGenerator, bool topBottom = false)
		{
			trigger.RegisterCallback<MouseEnterEvent>(ev =>
			{
				VisualElement inners = innersGenerator();
				if (inners == null)
					return;
				Create(trigger, inners, topBottom);
			});
		}

		static public void Bind(VisualElement trigger, Func<string> innersGenerator, bool topBottom = false)
		{
			trigger.RegisterCallback<MouseEnterEvent>(ev =>
			{
				var inners = innersGenerator();
				if (inners == null)
					return;
				var label = new Label();
				label.style.maxWidth = 256;
				label.style.whiteSpace = WhiteSpace.Normal;
				label.text = inners;
				label.styleSheets.Add(Resources.Load<StyleSheet>("UI/USS/Generic"));
				label.AddToClassList("MainWindow");
				Create(trigger, label, topBottom);
			});
		}

		static public void Create(VisualElement trigger, VisualElement inners, bool topBottom = false)
		{
			new TooltipController(trigger, inners, topBottom);
		}

		private TooltipController(VisualElement trigger, VisualElement inners, bool topBottom = false)
		{
			root = new VisualElement();
			this.trigger = trigger;
			this.inners = inners;
			this.topBottom = topBottom;

			root.userData = this;
			head = Utility.UI.Head(trigger);
			root.Add(inners);
			head.Add(root);

			trigger.RegisterCallback<MouseLeaveEvent>(OnHoverOff);
			update = head.schedule.Execute(UpdateIfNeeded).Every(15);
			Update();
		}

		void UpdateIfNeeded()
		{
			if (triggerBound != trigger.contentContainer.worldBound)
				Update();
			if (Utility.UI.Head(trigger) != head)
				OnHoverOff();
		}

		void Update()
		{
			triggerBound = trigger.contentContainer.worldBound;
			root.style.position = Position.Absolute;

			if (topBottom)
			{
				// Align horizontally with the trigger
				var triggerMidX = triggerBound.x + triggerBound.width / 2;
				if (triggerMidX < Screen.width / 3)
				{
					root.style.left = new StyleLength(new Length(triggerBound.x, LengthUnit.Pixel));
					root.style.right = new StyleLength(StyleKeyword.Auto);
					inners.style.left = new StyleLength(StyleKeyword.Auto);
				}
				else if (triggerMidX > Screen.width * 2 / 3)
				{
					root.style.left = new StyleLength(StyleKeyword.Auto);
					root.style.right = new StyleLength(new Length(Math.Max(0, Screen.width - triggerBound.x - triggerBound.width), LengthUnit.Pixel));
					inners.style.left = new StyleLength(StyleKeyword.Auto);
				}
				else
				{
					root.style.left = new StyleLength(new Length(triggerBound.x + triggerBound.width / 2, LengthUnit.Pixel));
					root.style.right = new StyleLength(StyleKeyword.Auto);
					inners.style.left = new StyleLength(new Length(-50, LengthUnit.Percent));
				}

				inners.style.top = new StyleLength(StyleKeyword.Auto);

				// Place below if trigger is in the upper half, above otherwise
				var triggerMidY = triggerBound.y + triggerBound.height / 2;
				if (triggerMidY < Screen.height / 2)
				{
					root.style.top = new StyleLength(new Length(triggerBound.y + triggerBound.height, LengthUnit.Pixel));
					root.style.bottom = new StyleLength(StyleKeyword.Auto);
				}
				else
				{
					root.style.top = new StyleLength(StyleKeyword.Auto);
					root.style.bottom = new StyleLength(new Length(Screen.height - triggerBound.y, LengthUnit.Pixel));
				}
			}
			else
			{
				// Align vertically with the trigger and place to its left or right
				if (triggerBound.x + triggerBound.width > Screen.width / 2)
					root.style.right = new StyleLength(new Length(Screen.width - triggerBound.x, LengthUnit.Pixel));
				else
					root.style.left = new StyleLength(new Length(triggerBound.x + triggerBound.width, LengthUnit.Pixel));

				var triggerHeight = triggerBound.y + triggerBound.height / 2;
				if (triggerHeight < Screen.height / 3)
				{
					root.style.top = new StyleLength(new Length(Math.Max(0, triggerBound.y), LengthUnit.Pixel));
					root.style.bottom = new StyleLength(StyleKeyword.Auto);
					inners.style.top = new StyleLength(StyleKeyword.Auto);
				}
				else if (triggerHeight > Screen.height * 2 / 3)
				{
					root.style.top = new StyleLength(StyleKeyword.Auto);
					root.style.bottom = new StyleLength(new Length(Math.Max(0, Screen.height - triggerBound.y - triggerBound.height), LengthUnit.Pixel));
					inners.style.top = new StyleLength(StyleKeyword.Auto);
				}
				else
				{
					root.style.top = new StyleLength(new Length(triggerBound.y + triggerBound.height / 2, LengthUnit.Pixel));
					root.style.bottom = new StyleLength(StyleKeyword.Auto);
					inners.style.top = new StyleLength(new Length(-50, LengthUnit.Percent));
				}
			}
		}

		private void OnHoverOff(MouseLeaveEvent ev)
		{
			OnHoverOff();
		}

		private void OnHoverOff()
		{
			root.Clear();
			trigger.UnregisterCallback<MouseLeaveEvent>(OnHoverOff);
			update?.Pause();
			update = null;
			head.Remove(root);
		}
	}
}
