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

    private UniformInteraction uniform;
    private Transform holdPoint;
    private GameObject heldGlass;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private Collider[] heldColliders;

    private GameObject beerStream;
    private bool pouring;
    private readonly bool[] filled = new bool[2];
    private readonly bool[] served = new bool[2];
    private GameObject serviceMarker;
    private BoxCollider serviceDesk;
    private readonly LineRenderer[] serviceRings = new LineRenderer[2];
    private Material markerMaterial;
    private Material completedMaterial;
    private Material liquidMaterial;
    private Material foamMaterial;
    private Material streamMaterial;
    private readonly GameObject[] filledVisuals = new GameObject[2];
    public bool TaskCompleted => served[0] && served[1];

    private string prompt = "";
    private GUIStyle textStyle;

    void Start()
    {
        uniform = GetComponent<UniformInteraction>();
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
        CreateServiceMarker();
    }

    void Update()
    {
        prompt = "";

        if (uniform == null || !uniform.isActiveAndEnabled
            || !uniform.ShiftCompleted || uniform.IsGuidePlaying)
            return;

        if (serviceMarker != null)
        {
            serviceMarker.SetActive(true);
            float width = TaskCompleted ? 0.035f : 0.025f + Mathf.Sin(Time.time * 3f) * 0.005f;
            foreach (LineRenderer ring in serviceRings)
                if (ring != null) ring.widthMultiplier = width;
        }

        if (TaskCompleted || Cursor.lockState != CursorLockMode.Locked)
            return;

        if (pouring)
        {
            prompt = "Pouring beer...";
            return;
        }

        if (heldGlass == null)
        {
            CheckGlassPickup();
        }
        else
        {
            if (IsAimingAtServiceMarker())
            {
                int index = HeldGlassIndex();
                prompt = filled[index]
                    ? "Left Click - Serve beer on the glowing marker"
                    : "Fill this glass at the beer tap before serving.";
                if (PressedLeftClick() && filled[index])
                    ServeGlass(index);
                return;
            }

            CheckPouring();

            if (PressedLeftClick() && !pouring)
                DropGlass();
        }
    }

    void CheckGlassPickup()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return;

        Transform target = hit.collider.transform;

        if (!served[0] && schoonerGlass != null && target.IsChildOf(schoonerGlass.transform))
        {
            prompt = "Left Click - Pick up schooner";

            if (PressedLeftClick())
                PickUpGlass(schoonerGlass);

            return;
        }

        if (!served[1] && pintGlass != null && target.IsChildOf(pintGlass.transform))
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
        if (filled[HeldGlassIndex()])
        {
            prompt = "Beer ready! Find the glowing SERVE marker. Left Click elsewhere to return glass.";
            return;
        }

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

        int index = HeldGlassIndex();
        filled[index] = true;
        CreateFilledVisual(index);
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

        streamMaterial = new Material(shader);
        streamMaterial.color = new Color(1f, 0.65f, 0.05f, 0.85f);

        renderer.sharedMaterial = streamMaterial;

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

    int HeldGlassIndex()
    {
        return heldGlass == schoonerGlass ? 0 : 1;
    }

    bool IsAimingAtServiceMarker()
    {
        if (serviceMarker == null)
            return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return false;

        if (hit.transform.IsChildOf(serviceMarker.transform))
            return true;

        // 링뿐 아니라 링이 놓인 상판을 눌러도 서빙합니다.
        Vector3 offset = hit.point - serviceMarker.transform.position;
        return hit.collider == serviceDesk && Mathf.Abs(offset.x) <= 0.85f
            && Mathf.Abs(offset.z) <= 0.6f && Mathf.Abs(offset.y) <= 0.2f;
    }

    void ServeGlass(int index)
    {
        if (!filled[index] || served[index] || pouring || heldGlass == null)
            return;

        // 원래 잔의 자식 메시 오프셋을 고려하여 잔 바닥이 표식 위에 오도록 정렬합니다.
        Vector3 slot = serviceMarker.transform.TransformPoint(new Vector3(index == 0 ? -0.38f : 0.38f, 0f, 0f));
        heldGlass.transform.SetParent(originalParent, false);
        heldGlass.transform.localRotation = originalLocalRotation;
        heldGlass.transform.localScale = originalLocalScale;
        heldGlass.transform.position = slot;
        Renderer[] renderers = heldGlass.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            heldGlass.transform.position += new Vector3(slot.x - bounds.center.x,
                slot.y - bounds.min.y, slot.z - bounds.center.z);
        }

        // 서빙된 잔은 다시 집거나 중복 제출할 수 없습니다.
        served[index] = true;
        serviceRings[index].sharedMaterial = completedMaterial;
        heldGlass = null;
        heldColliders = null;
        prompt = TaskCompleted ? "Order complete! Both beers served." : "Beer served! Prepare the other glass.";
    }

    void CreateServiceMarker()
    {
        BoxCollider desk = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>())
            {
                if (collider.gameObject.activeInHierarchy && collider.name == "BeerStage_BarCounterTop")
                {
                    desk = collider;
                    break;
                }
            }
            if (desk != null)
                break;
        }
        if (desk == null)
        {
            Debug.LogError("BeerGlassPour: BeerStage_BarCounterTop 데스크를 찾지 못해 서빙 표식을 만들 수 없습니다.");
            return;
        }

        serviceDesk = desk;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        markerMaterial = new Material(shader) { color = new Color(0.1f, 0.85f, 1f) };
        completedMaterial = new Material(shader) { color = new Color(0.25f, 1f, 0.35f) };
        liquidMaterial = new Material(shader) { color = new Color(1f, 0.62f, 0.06f) };
        foamMaterial = new Material(shader) { color = new Color(1f, 0.95f, 0.8f) };

        // 현재 씬의 실제 데스크 앞쪽에 생성합니다. 원본 Transform은 변경하지 않습니다.
        serviceMarker = new GameObject("BeerService_Marker_Runtime");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(serviceMarker, gameObject.scene);
        serviceMarker.transform.position = desk.transform.TransformPoint(desk.center
            + new Vector3(0f, desk.size.y * 0.5f, -desk.size.z * 0.25f)) + Vector3.up * 0.02f;
        // 탭 옆에서 다음 목표를 바로 볼 수 있도록 표식을 가까이 둡니다.
        if (beerNozzle != null)
        {
            float left = desk.transform.TransformPoint(desk.center - Vector3.right * desk.size.x * 0.5f).x;
            float right = desk.transform.TransformPoint(desk.center + Vector3.right * desk.size.x * 0.5f).x;
            Vector3 position = serviceMarker.transform.position;
            position.x = Mathf.Clamp(beerNozzle.position.x - 1f, Mathf.Min(left, right) + 0.8f,
                Mathf.Max(left, right) - 0.8f);
            serviceMarker.transform.position = position;
        }
        BoxCollider zone = serviceMarker.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(0f, 0.18f, 0f);
        zone.size = new Vector3(1.5f, 0.4f, 0.7f);

        for (int index = 0; index < 2; index++)
        {
            GameObject ringObject = new GameObject(index == 0 ? "Schooner_ServeSlot" : "Pint_ServeSlot");
            ringObject.transform.SetParent(serviceMarker.transform, false);
            ringObject.transform.localPosition = new Vector3(index == 0 ? -0.38f : 0.38f, 0f, 0f);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.025f;
            ring.sharedMaterial = markerMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            for (int segment = 0; segment < 48; segment++)
            {
                float angle = segment * Mathf.PI * 2f / 48;
                ring.SetPosition(segment, new Vector3(Mathf.Cos(angle) * 0.27f, 0f, Mathf.Sin(angle) * 0.27f));
            }
            serviceRings[index] = ring;
        }
        serviceMarker.SetActive(false);
    }

    void CreateFilledVisual(int index)
    {
        if (liquidMaterial == null || filledVisuals[index] != null)
            return;
        Renderer outer = heldGlass.GetComponentInChildren<Renderer>();
        if (outer == null) return;
        GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = "FilledBeer_Runtime";
        liquid.transform.SetParent(outer.transform, false);
        liquid.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        liquid.transform.localScale = new Vector3(0.78f, 0.7f, 0.78f);
        Collider liquidCollider = liquid.GetComponent<Collider>();
        liquidCollider.enabled = false;
        Destroy(liquidCollider);
        liquid.GetComponent<Renderer>().sharedMaterial = liquidMaterial;
        GameObject foam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        foam.name = "BeerFoam_Runtime";
        foam.transform.SetParent(liquid.transform, false);
        foam.transform.localPosition = new Vector3(0f, 1f, 0f);
        foam.transform.localScale = new Vector3(1f, 0.055f, 1f);
        Collider foamCollider = foam.GetComponent<Collider>();
        foamCollider.enabled = false;
        Destroy(foamCollider);
        foam.GetComponent<Renderer>().sharedMaterial = foamMaterial;
        filledVisuals[index] = liquid;
    }

    void OnDestroy()
    {
        if (serviceMarker != null) Destroy(serviceMarker);
        if (beerStream != null) Destroy(beerStream);
        if (holdPoint != null) Destroy(holdPoint.gameObject);
        foreach (GameObject visual in filledVisuals)
            if (visual != null) Destroy(visual);
        if (markerMaterial != null) Destroy(markerMaterial);
        if (completedMaterial != null) Destroy(completedMaterial);
        if (liquidMaterial != null) Destroy(liquidMaterial);
        if (foamMaterial != null) Destroy(foamMaterial);
        if (streamMaterial != null) Destroy(streamMaterial);
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
        if (uniform == null || !uniform.ShiftCompleted)
            return;

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 20;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.wordWrap = true;
            textStyle.normal.textColor = Color.white;
        }

        int count = (served[0] ? 1 : 0) + (served[1] ? 1 : 0);
        Rect taskArea = new Rect(Mathf.Max(15f, Screen.width - 405f), 15f, 390f, 105f);
        GUI.Box(taskArea, "");
        GUI.Label(taskArea, TaskCompleted
            ? "TASK COMPLETE!\nBoth beers served."
            : "SERVE TWO BEERS  " + count + "/2\nSchooner: " + GlassStatus(0) + "   Pint: " + GlassStatus(1), textStyle);

        string instruction;
        if (TaskCompleted)
            instruction = "TASK COMPLETE! Both beers served.";
        else if (pouring)
            instruction = "2. POURING BEER...";
        else if (heldGlass == null)
            instruction = "1. Pick up a glass with LEFT CLICK";
        else if (!filled[HeldGlassIndex()])
            instruction = "2. Aim at the beer tap and press E to fill the glass";
        else
            instruction = "3. BEER READY! Aim at the glowing SERVE spot and LEFT CLICK to place";

        float panelWidth = Mathf.Min(650f, Screen.width - 30f);
        Rect instructionArea = new Rect((Screen.width - panelWidth) * 0.5f, 130f, panelWidth, 85f);
        GUI.Box(instructionArea, "");
        GUI.Label(instructionArea, "PICK UP > POUR > SERVE\n" + instruction, textStyle);

        if (serviceMarker != null && serviceMarker.activeInHierarchy)
        {
            Vector3 screen = playerCamera.WorldToScreenPoint(serviceMarker.transform.position + Vector3.up * 0.5f);
            bool readyToServe = heldGlass != null && filled[HeldGlassIndex()] && !pouring;
            bool onScreen = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width
                && screen.y >= 0f && screen.y <= Screen.height;
            if (onScreen || readyToServe)
            {
                if (screen.z < 0f)
                {
                    screen.x = Screen.width - screen.x;
                    screen.y = Screen.height - screen.y;
                }
                float labelX = Mathf.Clamp(screen.x - 150f, 5f, Mathf.Max(5f, Screen.width - 305f));
                float labelY = Mathf.Clamp(Screen.height - screen.y - 25f, 225f,
                    Mathf.Max(225f, Screen.height - 90f));
                Rect markerArea = new Rect(labelX, labelY, 300f, 65f);
                GUI.Box(markerArea, "");
                GUI.Label(markerArea, TaskCompleted ? "SERVING COMPLETE"
                    : !onScreen ? "TURN TOWARD SERVE"
                    : readyToServe ? "SERVE HERE\nAim here + LEFT CLICK"
                    : "SERVE HERE  " + count + "/2", textStyle);
            }
        }

        GUI.Label(
            new Rect(0, Screen.height / 2f + 55f, Screen.width, 45f),
            prompt,
            textStyle
        );
    }

    string GlassStatus(int index)
    {
        return served[index] ? "SERVED" : filled[index] ? "READY" : "EMPTY";
    }

}