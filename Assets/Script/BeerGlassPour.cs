using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BeerGlassPour : MonoBehaviour
{
    public Camera playerCamera;

    public GameObject schoonerGlass;
    public GameObject pintGlass;

    public GameObject schoonerPourVisual;
    public GameObject pintPourVisual;

    public Transform beerNozzle;

    public float pickupDistance = 2.5f;
    public float nozzleDistance = 3f;
    public float nozzleAimAngle = 90f;
    public float pourDuration = 2.5f;
    public float beerStreamLength = 0.65f;

    public Vector3 holdLocalPosition = new Vector3(0.32f, -0.28f, 1.25f);
    public Vector3 heldGlassLocalRotation = new Vector3(0f, 0f, 0f);

    private Transform holdPoint;
    private GameObject heldGlass;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private Collider[] heldColliders;

    private GameObject beerStream;
    private bool pouring;

    private string prompt = "";
    private GUIStyle textStyle;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError("BeerGlassPour: Player Camera를 연결하세요.");
            enabled = false;
            return;
        }

        GameObject holdObject = new GameObject("BeerGlass_HoldPoint");
        holdObject.transform.SetParent(playerCamera.transform, false);
        holdObject.transform.localPosition = holdLocalPosition;
        holdObject.transform.localRotation = Quaternion.identity;
        holdPoint = holdObject.transform;

        PreparePourVisual(schoonerPourVisual);
        PreparePourVisual(pintPourVisual);

        CreateBeerStream();
    }

    void Update()
    {
        prompt = "";

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (heldGlass == null)
        {
            CheckGlassPickup();
        }
        else
        {
            CheckPouring();

            if (PressedLeftClick() && !pouring)
            {
                DropGlass();
            }
        }
    }

    void CheckGlassPickup()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
            return;

        Transform target = hit.collider.transform;

        if (schoonerGlass != null && target.IsChildOf(schoonerGlass.transform))
        {
            prompt = "Left Click - Pick up schooner";

            if (PressedLeftClick())
                PickUpGlass(schoonerGlass);

            return;
        }

        if (pintGlass != null && target.IsChildOf(pintGlass.transform))
        {
            prompt = "Left Click - Pick up pint";

            if (PressedLeftClick())
                PickUpGlass(pintGlass);

            return;
        }
    }

    void PickUpGlass(GameObject glass)
    {
        heldGlass = glass;

        originalParent = glass.transform.parent;
        originalLocalPosition = glass.transform.localPosition;
        originalLocalRotation = glass.transform.localRotation;
        originalLocalScale = glass.transform.localScale;

        heldColliders = glass.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in heldColliders)
            collider.enabled = false;

        glass.transform.SetParent(holdPoint, false);
        glass.transform.localPosition = Vector3.zero;
        glass.transform.localRotation = Quaternion.Euler(heldGlassLocalRotation);
        glass.transform.localScale = originalLocalScale;

        SetVisible(glass, true);
    }

    void DropGlass()
    {
        if (heldGlass == null)
            return;

        SetVisible(heldGlass, true);

        heldGlass.transform.SetParent(originalParent, false);
        heldGlass.transform.localPosition = originalLocalPosition;
        heldGlass.transform.localRotation = originalLocalRotation;
        heldGlass.transform.localScale = originalLocalScale;

        if (heldColliders != null)
        {
            foreach (Collider collider in heldColliders)
                collider.enabled = true;
        }

        heldGlass = null;
        heldColliders = null;
    }

    void CheckPouring()
    {
        if (beerNozzle == null)
        {
            prompt = "Beer nozzle is not connected.";
            return;
        }

        if (CanPour())
        {
            prompt = "E - Pour beer";

            if (PressedInteract() && !pouring)
                StartCoroutine(PourBeer());
        }
        else
        {
            prompt = "Move closer to the beer nozzle";
        }
    }

    bool CanPour()
    {
        Vector3 toNozzle = beerNozzle.position - playerCamera.transform.position;

        if (toNozzle.magnitude > nozzleDistance)
            return false;

        float angle = Vector3.Angle(playerCamera.transform.forward, toNozzle.normalized);

        return angle <= nozzleAimAngle;
    }

    IEnumerator PourBeer()
    {
        pouring = true;
        prompt = "";

        GameObject pourVisual = GetPourVisualForHeldGlass();

        if (pourVisual == null)
        {
            Debug.LogWarning("Pour visual glass가 연결되지 않았습니다.");
            pouring = false;
            yield break;
        }

        SetVisible(heldGlass, false);
        SetVisible(pourVisual, true);

        if (beerStream != null)
            beerStream.SetActive(true);

        float timer = 0f;

        while (timer < pourDuration)
        {
            timer += Time.deltaTime;
            PositionBeerStream();
            yield return null;
        }

        if (beerStream != null)
            beerStream.SetActive(false);

        SetVisible(pourVisual, false);
        SetVisible(heldGlass, true);

        pouring = false;
    }

    GameObject GetPourVisualForHeldGlass()
    {
        if (heldGlass == schoonerGlass)
            return schoonerPourVisual;

        if (heldGlass == pintGlass)
            return pintPourVisual;

        return null;
    }

    void PreparePourVisual(GameObject visual)
    {
        if (visual == null)
            return;

        visual.SetActive(true);
        SetVisible(visual, false);

        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
            collider.enabled = false;
    }

    void SetVisible(GameObject target, bool visible)
    {
        if (target == null)
            return;

        target.SetActive(true);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
            renderer.enabled = visible;
    }

    void CreateBeerStream()
    {
        beerStream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beerStream.name = "BeerStream_Runtime";

        Collider streamCollider = beerStream.GetComponent<Collider>();
        if (streamCollider != null)
            Destroy(streamCollider);

        Renderer renderer = beerStream.GetComponent<Renderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material beerMaterial = new Material(shader);
        beerMaterial.color = new Color(1f, 0.65f, 0.05f, 0.85f);

        renderer.material = beerMaterial;

        beerStream.SetActive(false);
    }

    void PositionBeerStream()
    {
        if (beerStream == null || beerNozzle == null)
            return;

        beerStream.transform.position =
            beerNozzle.position + Vector3.down * (beerStreamLength * 0.5f);

        beerStream.transform.rotation = Quaternion.identity;
        beerStream.transform.localScale =
            new Vector3(0.035f, beerStreamLength * 0.5f, 0.035f);
    }

    bool PressedLeftClick()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool PressedInteract()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null
            && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(prompt))
            return;

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 20;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.wordWrap = true;
            textStyle.normal.textColor = Color.white;
        }

        GUI.Label(
            new Rect(0, Screen.height / 2f + 55f, Screen.width, 45f),
            prompt,
            textStyle
        );
    }
}