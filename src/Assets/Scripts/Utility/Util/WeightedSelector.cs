using System;
using System.Collections.Generic;

public class WeightedSelector<T>
{
	public WeightedSelector( Func<int, int> randomGenerator )
	{
		randomGeneratorPred = randomGenerator;
	}

	public WeightedSelector( Utility.IRandom rng = null )
	{
		this.rng = rng;
	}

	public void SetRng( Utility.IRandom rng )
	{
		this.rng = rng;
	}

	public void Clear()
	{
		items.Clear();
		total = 0;
	}

	public void AddItem( T item, int weight )
	{
		if ( weight > 0 )
		{
			total += weight;
			items.Add( (item, total, weight) );
		}
	}

	private int GetRandom()
	{
		if ( items.Count <= 1 )
			return 0;

		if ( rng != null )
			return rng.Range( 0, total ) + 1;

		if ( randomGeneratorPred != null )
			return randomGeneratorPred( total ) + 1;

		return Utility.DefaultRng.Range( 0, total ) + 1;
	}

	int GetResultIdx()
	{
		if ( !HasResult() )
			return -1;

		if ( items.Count == 1 )
			return 0;

		var randomVal = GetRandom();
		var resultIdx = items.BinarySearch( (default( T ), randomVal, 0),
			Comparer<(T, int, int)>.Create( ( x, y ) => x.Item2.CompareTo( y.Item2 ) ) );

		if ( resultIdx < 0 )
		{
			resultIdx = ~resultIdx;
			// If insertion point is at the end, clamp to last valid index
			if ( resultIdx >= items.Count )
				resultIdx = items.Count - 1;
		}

		return resultIdx;
	}

	public T GetResult()
	{
		var resultIdx = GetResultIdx();
		if ( resultIdx < 0 || resultIdx >= items.Count )
			return default;
		return items[resultIdx].Item1;
	}

	public T TakeResult()
	{
		var resultIdx = GetResultIdx();
		if ( resultIdx < 0 || resultIdx >= items.Count )
			return default;
		var result = items[resultIdx];
		items.RemoveAt( resultIdx );
		total -= result.Item3;

		for ( int i = resultIdx; i < items.Count; i++ )
		{
			var item = items[i];
			items[i] = (item.Item1, item.Item2 - result.Item3, item.Item3);
		}

		return result.Item1;
	}

	public bool HasResult()
	{
		return items.Count > 0 && items[^1].Item2 > 0 && total > 0;
	}

	private readonly List<(T, int, int)> items = new();
	private int total = 0;
	private readonly Func<int, int> randomGeneratorPred;
	private Utility.IRandom rng;
}
