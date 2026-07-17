using UnityEngine;

public class FreezerPipe : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Enter");
    }
}
