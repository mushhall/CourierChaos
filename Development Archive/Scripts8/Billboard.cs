using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform cam;

    private void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main != null) cam = Camera.main.transform;
            return;
        }

        // Face the same way the camera looks, so text stays readable.
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.position);
    }
}
