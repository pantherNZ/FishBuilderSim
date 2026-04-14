using UnityEngine;
using UnityEngine.UIElements;

namespace Schema
{
	[CreateAssetMenu(fileName = "UIConstants", menuName = "FishBuilderSim/Constants/UIConstants")]
	public class UIConstants : ScriptableObject
	{
		public VisualTreeAsset slideShowTemplate;
		public VisualTreeAsset slideShowPageIndicatorTemplate;
		public VisualTreeAsset genericPopupTemplate;
		public VisualTreeAsset yesNoPopupTemplate;
		public VisualTreeAsset closePopupTemplate;
		public VisualTreeAsset toastTemplate;
		public VisualTreeAsset highlightTemplate;
	}
}