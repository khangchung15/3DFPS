using UnityEngine;

public class TipZone : MonoBehaviour
{
    [Tooltip("The UI GameObject to show while the player is inside the zone")]
    public GameObject tipUI;

    private void Start()
    {
        // Make sure the tip starts hidden
        if (tipUI != null)
            tipUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && tipUI != null)
            tipUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && tipUI != null)
            tipUI.SetActive(false);
    }
}