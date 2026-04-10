using UnityEngine;
using UnityEngine.UI;

public class MultiImageTargetGraphics : MonoBehaviour
{
    [SerializeField] private Graphic[] targetGraphics = null;
    public Graphic[] GetTargetGraphics => targetGraphics;
}