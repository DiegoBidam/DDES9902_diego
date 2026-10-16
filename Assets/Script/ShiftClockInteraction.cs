using UnityEngine;

public class ShiftClockInteraction : MonoBehaviour
{
    public Transform beerStageSpawn;
    public GameObject clockGuide;

    private bool shiftStarted = false;

    private void Update()
    {
        if (shiftStarted)
        {
            return;
        }

        if (clockGuide == null || beerStageSpawn == null)
        {
            return;
        }

        if (!clockGuide.activeInHierarchy)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            StartShift();
        }
    }

    private void StartShift()
    {
        shiftStarted = true;

        if (clockGuide != null)
        {
            clockGuide.SetActive(false);
        }

        CharacterController controller = GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = beerStageSpawn.position;
        transform.rotation = beerStageSpawn.rotation;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}