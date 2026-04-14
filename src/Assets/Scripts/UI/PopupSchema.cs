using System;
using Schema.Items;
using UnityEngine;

namespace Schema
{
	[CreateAssetMenu(fileName = "Popup", menuName = "FishBuilderSim/Popup")]
	public class PopupSchema : BaseDataSchema
	{
		public string header;
		[TextArea]
		public string body;
		public string yes;
		public string no;
		public string cancel;
		public Sprite image;
	}
}
