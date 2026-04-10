using UnityEngine;

public class PositionClamp : MonoBehaviour
{
    public bool shouldClampX = false;
    public Interval clampX = new Interval(0, 0);
    public bool shouldClampY = false;
    public Interval clampY = new Interval(0, 0);
    public bool shouldClampZ = false;
    public Interval clampZ = new Interval(0, 0);

    private void FixedUpdate()
    {
        Vector3 position = transform.position;

        if (shouldClampX)
        {
            position.x = Mathf.Clamp(position.x, clampX.Min, clampX.Max);
        }

        if (shouldClampY)
        {
            position.y = Mathf.Clamp(position.y, clampY.Min, clampY.Max);
        }

        if (shouldClampZ)
        {
            position.z = Mathf.Clamp(position.z, clampZ.Min, clampZ.Max);
        }

        transform.position = position;
    }
}