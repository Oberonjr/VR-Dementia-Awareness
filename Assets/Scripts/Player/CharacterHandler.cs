using UnityEngine;
using UnityEngine.XR;

public class CharacterHandler : MonoBehaviour
{
    [SerializeField] private GameObject VRCharacter;
    [SerializeField] private GameObject FPSCharacter;

    private bool isVRActive = false;

    private void Start()
    {
        isVRActive = XRSettings.isDeviceActive;
        UpdateCharacter();
    }

    private void Update()
    {
        bool updatedVRStatus = XRSettings.isDeviceActive;

        if (updatedVRStatus != isVRActive)
        {
            isVRActive = updatedVRStatus;
            UpdateCharacter();
        }
    }

    private void UpdateCharacter()
    {
        VRCharacter.SetActive(isVRActive);
        FPSCharacter.SetActive(!isVRActive);
    }
}
