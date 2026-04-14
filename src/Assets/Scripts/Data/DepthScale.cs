using UnityEngine;

namespace Runtime.Terrain
{
	// Depth is the value used for scaling difficulty etc.
	// It is a distance from the origin point of a layer and the base added difficulty of that layer

	// If you need listeners for depth scale changes, create a DepthScale instance instead of using this lighter cost struct
	public struct DepthScaleData
	{
		public int depth;

		public DepthScaleData(int depth)
		{
			this.depth = depth;
		}

		public DepthScaleData(Vector2Int globalVoxelPos)
		{
			depth = Mathf.FloorToInt(globalVoxelPos.magnitude);
		}
	}

	public class DepthScale
	{
		ReadWriteProperty<int> _depth = new();

		public Property<int> depth => _depth;
		public static implicit operator DepthScaleData(DepthScale depthScale) => new DepthScaleData(depthScale.depth);

		public DepthScale(Property<int> depth)
		{
			depth.Bind(OnDepthChanged);
		}

		public DepthScale(int depth)
		{
			OnDepthChanged(depth);
		}

		public DepthScale(Vector2Int globalVoxelPos)
		{
			OnDepthChanged(Mathf.FloorToInt(globalVoxelPos.magnitude));
		}

		public void SetGlobalVoxelPos(Vector2Int globalVoxelPos)
		{
			OnDepthChanged(Mathf.FloorToInt(globalVoxelPos.magnitude));
		}

		void OnDepthChanged(int depth)
		{
			_depth.SetValue(depth);
		}
	}
}
