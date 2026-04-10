using UnityEngine;

public class IndependentRotation : MonoBehaviour
{
    public Quaternion initialRotation;

    void LateUpdate()
    {
        // Reset the child's local rotation to its initial value
        transform.rotation = initialRotation;
    }
}