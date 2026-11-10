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
    private CocktailEquipment cocktailEquipment;
    private RecipeCards recipeCards;
    public bool IsRecipeOpen => recipeCards != null && recipeCards.BlocksInput;
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
        cocktailEquipment = new CocktailEquipment(this);
        recipeCards = new RecipeCards(this, cocktailEquipment.Table);
    }

    void Update()
    {
        prompt = "";

        if (IsRecipeOpen)
        {
            if (recipeCards.IsOpen && PressedRecipeClose()) recipeCards.Close();
            return;
        }

        if (uniform == null || !uniform.isActiveAndEnabled
            || !uniform.ShiftCompleted || uniform.IsGuidePlaying)
            return;

        if (recipeCards != null) recipeCards.SetAvailable(TaskCompleted);

        if (serviceMarker != null)
        {
            serviceMarker.SetActive(true);
            float width = TaskCompleted ? 0.035f : 0.025f + Mathf.Sin(Time.time * 3f) * 0.005f;
            foreach (LineRenderer ring in serviceRings)
                if (ring != null) ring.widthMultiplier = width;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (recipeCards != null && recipeCards.HandleInput(PressedLeftClick(), out string recipePrompt))
        {
            prompt = recipePrompt;
            return;
        }

        if (heldGlass == null && cocktailEquipment != null
            && cocktailEquipment.HandleInput(PressedLeftClick(), out string equipmentPrompt))
        {
            prompt = equipmentPrompt;
            return;
        }

        if (TaskCompleted)
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
        if (recipeCards != null) recipeCards.Dispose();
        if (cocktailEquipment != null) cocktailEquipment.Dispose();
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

    bool PressedRecipeClose()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    void OnGUI()
    {
        if (uniform == null || !uniform.ShiftCompleted)
            return;

        if (recipeCards != null && recipeCards.IsOpen)
        {
            recipeCards.DrawOverlay();
            return;
        }

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
        if (cocktailEquipment != null && cocktailEquipment.IsHolding)
            instruction = "COCKTAIL TOOLS: " + cocktailEquipment.HeldName + "\nLEFT CLICK to put it back on the table";
        else if (TaskCompleted)
            instruction = "BEER TASK COMPLETE! Explore the cocktail tools on the rear table.";
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
        bool cocktailFocus = TaskCompleted || (cocktailEquipment != null && cocktailEquipment.IsHolding);
        GUI.Label(instructionArea, (cocktailFocus ? "COCKTAIL STATION\n" : "PICK UP > POUR > SERVE\n") + instruction, textStyle);
        if (cocktailEquipment != null) cocktailEquipment.DrawLabels(textStyle);
        if (recipeCards != null) recipeCards.DrawNoteLabels(textStyle);

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

    // 동기화 대상인 이 파일에 포함해 새 씬/프리팹/Inspector 연결 없이 장비를 만듭니다.
    private sealed class CocktailEquipment
    {
        private readonly BeerGlassPour owner;
        public BoxCollider Table { get; private set; }
        private GameObject root;
        private readonly GameObject[] tools = new GameObject[3];
        private readonly Vector3[] slots = new Vector3[3];
        private readonly string[] names = { "Large shaker tin", "Small shaker tin", "Jigger: 1 oz / 0.5 oz" };
        private readonly float[] heights = { 0.27f, 0.20f, 0.115f };
        private readonly System.Collections.Generic.List<Mesh> meshes = new System.Collections.Generic.List<Mesh>();
        private Material steel;
        private Material rubber;
        private int held = -1;
        public bool IsHolding => held >= 0;
        public string HeldName => IsHolding ? names[held] : "";

        public CocktailEquipment(BeerGlassPour owner)
        {
            this.owner = owner;
            BoxCollider table = null;
            foreach (GameObject sceneRoot in owner.gameObject.scene.GetRootGameObjects())
            {
                foreach (BoxCollider collider in sceneRoot.GetComponentsInChildren<BoxCollider>())
                {
                    if (collider.gameObject.activeInHierarchy && collider.name == "GlassTable_Top")
                    {
                        table = collider;
                        break;
                    }
                }
                if (table != null) break;
            }
            if (table == null)
            {
                Debug.LogError("CocktailEquipment: 뒤쪽 GlassTable_Top 테이블을 찾지 못했습니다.");
                return;
            }
            Table = table;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("CocktailEquipment: 금속 재질용 셰이더를 찾지 못했습니다.");
                return;
            }
            steel = new Material(shader) { color = new Color(0.72f, 0.78f, 0.82f) };
            steel.SetFloat("_Metallic", 0.85f);
            steel.SetFloat("_Smoothness", 0.78f);
            rubber = new Material(shader) { color = new Color(0.025f, 0.045f, 0.05f) };
            rubber.SetFloat("_Smoothness", 0.2f);

            root = new GameObject("CocktailEquipment_Runtime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            root.transform.position = table.transform.TransformPoint(table.center
                + new Vector3(0f, table.size.y * 0.5f, -table.size.z * 0.12f)) + Vector3.up * 0.012f;
            root.transform.rotation = table.transform.rotation;
            GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "Cocktail_BarMat";
            mat.transform.SetParent(root.transform, false);
            mat.transform.localPosition = new Vector3(0f, -0.006f, 0f);
            mat.transform.localScale = new Vector3(1.65f, 0.012f, 0.42f);
            mat.GetComponent<Renderer>().sharedMaterial = rubber;
            Collider matCollider = mat.GetComponent<Collider>();
            matCollider.enabled = false;
            UnityEngine.Object.Destroy(matCollider);

            CreateTin(0, 0.041f, 0.055f);
            CreateTin(1, 0.034f, 0.047f);
            CreateJigger();
        }

        private GameObject CreateTool(int index, string objectName, float diameter)
        {
            GameObject tool = new GameObject(objectName);
            tool.transform.SetParent(root.transform, false);
            slots[index] = new Vector3((index - 1) * 0.48f, 0f, 0f);
            tool.transform.localPosition = slots[index];
            BoxCollider collider = tool.AddComponent<BoxCollider>();
            collider.center = Vector3.up * heights[index] * 0.5f;
            collider.size = new Vector3(diameter, heights[index], diameter);
            tools[index] = tool;
            return tool;
        }

        private void CreateTin(int index, float bottomRadius, float topRadius)
        {
            float height = heights[index];
            GameObject tool = CreateTool(index, index == 0 ? "Shaker_LargeTin" : "Shaker_SmallTin", topRadius * 2f);
            // 외벽 → 둥근 테두리 → 내벽 → 바닥으로 이어지는 열린 컵입니다.
            Vector2[] profile = {
                new Vector2(0f, 0f), new Vector2(bottomRadius, 0f),
                new Vector2(bottomRadius + 0.002f, 0.012f),
                new Vector2(topRadius - 0.001f, height - 0.008f),
                new Vector2(topRadius, height - 0.003f), new Vector2(topRadius, height),
                new Vector2(topRadius - 0.003f, height),
                new Vector2(bottomRadius - 0.001f, 0.008f),
                new Vector2(0f, 0.006f), new Vector2(0f, 0f)
            };
            AddLathedMesh(tool.transform, "HollowStainlessTin", profile);
        }

        private void CreateJigger()
        {
            GameObject tool = CreateTool(2, "Jigger_1oz_0.5oz", 0.076f);
            GameObject large = new GameObject("Cup_1oz");
            large.transform.SetParent(tool.transform, false);
            large.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            AddLathedMesh(large.transform, "OneOunceCup", new[] {
                new Vector2(0f, 0f), new Vector2(0.012f, 0f),
                new Vector2(0.018f, 0.012f), new Vector2(0.036f, 0.061f),
                new Vector2(0.036f, 0.065f), new Vector2(0.033f, 0.065f),
                new Vector2(0.015f, 0.013f), new Vector2(0f, 0.006f), new Vector2(0f, 0f)
            });
            GameObject small = new GameObject("Cup_0.5oz");
            small.transform.SetParent(tool.transform, false);
            small.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            small.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            AddLathedMesh(small.transform, "HalfOunceCup", new[] {
                new Vector2(0f, 0f), new Vector2(0.012f, 0f),
                new Vector2(0.028f, 0.046f), new Vector2(0.028f, 0.05f),
                new Vector2(0.025f, 0.05f), new Vector2(0.009f, 0.009f),
                new Vector2(0f, 0.006f), new Vector2(0f, 0f)
            });
        }

        private void AddLathedMesh(Transform parent, string name, Vector2[] profile)
        {
            const int segments = 64;
            Vector3[] vertices = new Vector3[profile.Length * (segments + 1)];
            int[] triangles = new int[(profile.Length - 1) * segments * 6];
            int triangle = 0;
            for (int row = 0; row < profile.Length; row++)
            {
                for (int segment = 0; segment <= segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    int vertex = row * (segments + 1) + segment;
                    vertices[vertex] = new Vector3(Mathf.Cos(angle) * profile[row].x,
                        profile[row].y, Mathf.Sin(angle) * profile[row].x);
                    if (row == profile.Length - 1 || segment == segments) continue;
                    int next = vertex + segments + 1;
                    triangles[triangle++] = vertex;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next + 1;
                    triangles[triangle++] = vertex;
                    triangles[triangle++] = next + 1;
                    triangles[triangle++] = vertex + 1;
                }
            }
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            GameObject surface = new GameObject(name);
            surface.transform.SetParent(parent, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterial = steel;
        }

        public bool HandleInput(bool clicked, out string prompt)
        {
            prompt = "";
            if (root == null) return false;
            if (IsHolding)
            {
                prompt = "LEFT CLICK - Put " + names[held] + " back on the table";
                if (clicked)
                {
                    Transform tool = tools[held].transform;
                    tool.SetParent(root.transform, false);
                    tool.localPosition = slots[held];
                    tool.localRotation = Quaternion.identity;
                    tool.GetComponent<Collider>().enabled = true;
                    held = -1;
                }
                return true;
            }
            Ray ray = owner.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, owner.pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            for (int index = 0; index < tools.Length; index++)
            {
                if (tools[index] == null || !hit.transform.IsChildOf(tools[index].transform)) continue;
                prompt = "LEFT CLICK - Pick up " + names[index];
                if (clicked)
                {
                    held = index;
                    Transform tool = tools[index].transform;
                    tool.GetComponent<Collider>().enabled = false;
                    tool.SetParent(owner.playerCamera.transform, false);
                    tool.localPosition = new Vector3(0.24f, -0.3f, 0.65f);
                    tool.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                }
                return true;
            }
            return false;
        }

        public void DrawLabels(GUIStyle style)
        {
            if (root == null) return;
            for (int index = 0; index < tools.Length; index++)
            {
                if (tools[index] == null || held == index) continue;
                Vector3 point = tools[index].transform.position + Vector3.up * (heights[index] + 0.06f);
                if (Vector3.Distance(owner.playerCamera.transform.position, point) > 3.5f) continue;
                Vector3 screen = owner.playerCamera.WorldToScreenPoint(point);
                if (screen.z <= 0f) continue;
                GUI.Label(new Rect(screen.x - 115f, Screen.height - screen.y - 20f, 230f, 45f), names[index], style);
            }
        }

        public void Dispose()
        {
            if (IsHolding && tools[held] != null) UnityEngine.Object.Destroy(tools[held]);
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            if (steel != null) UnityEngine.Object.Destroy(steel);
            if (rubber != null) UnityEngine.Object.Destroy(rubber);
        }
    }

    private sealed class RecipeCards
    {
        private readonly BeerGlassPour owner;
        private GameObject root;
        private readonly GameObject[] notes = new GameObject[3];
        private readonly Material[] paper = new Material[3];
        private readonly string[] titles = { "Gin & Tonic", "네그로니", "롱아일랜드 아이스티" };
        private readonly string[] englishTitles = { "GIN & TONIC", "NEGRONI", "LONG ISLAND\nICED TEA" };
        private readonly string[] ingredients = {
            "진 1 oz\n토닉워터: 잔을 채울 만큼\n얼음",
            "스위트 베르무트 1 oz\n진 1 oz\n캄파리 1 oz\n얼음",
            "럼 0.5 oz\n진 0.5 oz\n보드카 0.5 oz\n트리플섹 0.5 oz\n레몬주스 1 oz\n콜라: 나머지를 채울 만큼\n얼음"
        };
        private readonly string[] steps = {
            "1. 언더락 글라스를 준비하세요.\n2. 진 1 oz를 정확히 계량해서 부으세요.\n3. 토닉워터로 잔을 채우세요.\n4. 얼음을 넣고 서빙하세요.",
            "1. 올드패션 글라스를 선택하세요.\n2. 스위트 베르무트, 진, 캄파리를 각각 1 oz씩 계량해서 부으세요.\n3. 얼음을 넣고 충분히 저어주세요.",
            "1. 롱드링크 글라스를 준비하세요.\n2. 럼, 진, 보드카, 트리플섹을 각각 0.5 oz씩 쉐이커에 넣으세요.\n3. 얼음을 넣고 충분히 쉐이킹해주세요.\n4. 얼음을 넣은 롱드링크 글라스에 부으세요.\n5. 위에 레몬주스 1 oz를 추가하세요.\n6. 나머지는 콜라로 톱업해주세요."
        };
        private Font font;
        private bool ownsFont;
        private int selected = -1;
        private int closeFrame = -1;
        private Vector2 scroll;
        private CursorLockMode previousLock;
        private bool previousCursorVisible;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        public bool IsOpen => selected >= 0;
        public bool BlocksInput => IsOpen || closeFrame == Time.frameCount;

        public RecipeCards(BeerGlassPour owner, BoxCollider table)
        {
            this.owner = owner;
            if (table == null) return;
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = fallback;
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string preferred in new[] { "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Malgun Gothic", "Arial Unicode MS" })
            {
                if (!System.Array.Exists(installed, name => string.Equals(name, preferred, System.StringComparison.OrdinalIgnoreCase)))
                    continue;
                Font candidate = Font.CreateDynamicFontFromOSFont(preferred, 22);
                if (candidate == null) continue;
                font = candidate;
                ownsFont = true;
                break;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;
            root = new GameObject("CocktailRecipeNotes_Runtime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            root.transform.position = table.transform.TransformPoint(table.center
                + Vector3.up * table.size.y * 0.5f) + Vector3.up * 0.004f;
            root.transform.rotation = table.transform.rotation;
            Color[] colors = { new Color(1f, 0.93f, 0.63f), new Color(1f, 0.81f, 0.7f), new Color(0.78f, 0.92f, 0.85f) };
            for (int index = 0; index < 3; index++)
            {
                GameObject note = GameObject.CreatePrimitive(PrimitiveType.Cube);
                note.name = "RecipeNote_" + (index + 1);
                note.transform.SetParent(root.transform, false);
                note.transform.localPosition = new Vector3(-1.9f + index * 0.43f, 0f, 0f);
                // 종이만 얇게 만들고 글씨는 크기가 1인 별도 오브젝트에 둡니다.
                note.transform.localScale = new Vector3(0.30f, 0.003f, 0.36f);
                paper[index] = new Material(shader) { color = colors[index] };
                note.GetComponent<Renderer>().sharedMaterial = paper[index];
                notes[index] = note;
                GameObject writing = new GameObject("WrittenRecipeTitle");
                writing.transform.SetParent(root.transform, false);
                writing.transform.localPosition = note.transform.localPosition + new Vector3(-0.13f, 0.003f, 0.15f);
                writing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                TextMesh text = writing.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = 48;
                text.characterSize = 0.022f;
                text.anchor = TextAnchor.UpperLeft;
                text.color = new Color(0.12f, 0.18f, 0.2f);
                text.text = "0" + (index + 1) + "\n" + englishTitles[index] + "\n\nRECIPE\nCLICK TO READ";
                writing.GetComponent<Renderer>().sharedMaterial = font.material;
            }
            root.SetActive(false);
        }

        public void SetAvailable(bool available)
        {
            if (root != null && root.activeSelf != available) root.SetActive(available);
        }

        public bool HandleInput(bool clicked, out string prompt)
        {
            prompt = "";
            if (root == null || !root.activeInHierarchy || BlocksInput) return false;
            Ray ray = owner.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, owner.pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            for (int index = 0; index < notes.Length; index++)
            {
                if (hit.collider.gameObject != notes[index]) continue;
                prompt = "LEFT CLICK - Read recipe " + (index + 1);
                if (clicked)
                {
                    previousLock = Cursor.lockState;
                    previousCursorVisible = Cursor.visible;
                    selected = index;
                    scroll = Vector2.zero;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return true;
            }
            return false;
        }

        public void Close()
        {
            if (!IsOpen) return;
            selected = -1;
            closeFrame = Time.frameCount;
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursorVisible;
        }

        public void DrawNoteLabels(GUIStyle style)
        {
            if (root == null || !root.activeInHierarchy) return;
            for (int index = 0; index < notes.Length; index++)
            {
                Vector3 point = notes[index].transform.position + Vector3.up * 0.06f;
                if (Vector3.Distance(owner.playerCamera.transform.position, point) > owner.pickupDistance) continue;
                Vector3 screen = owner.playerCamera.WorldToScreenPoint(point);
                if (screen.z > 0f)
                    GUI.Label(new Rect(screen.x - 75f, Screen.height - screen.y - 30f, 150f, 32f),
                        "RECIPE 0" + (index + 1), style);
            }
        }

        public void DrawOverlay()
        {
            if (!IsOpen) return;
            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, wordWrap = true };
                bodyStyle.normal.textColor = new Color(0.13f, 0.19f, 0.2f);
                titleStyle = new GUIStyle(bodyStyle) { fontSize = 30, fontStyle = FontStyle.Bold };
                sectionStyle = new GUIStyle(bodyStyle) { fontSize = 22, fontStyle = FontStyle.Bold };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
            }
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            float width = Mathf.Min(760f, Screen.width - 24f);
            float height = Mathf.Min(650f, Screen.height - 24f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.color = new Color(1f, 0.97f, 0.87f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(new Rect(panel.x + 26f, panel.y + 18f, width - 52f, 55f), titles[selected], titleStyle);
            GUI.Label(new Rect(panel.x + 26f, panel.y + 76f, width - 52f, 32f), "레시피 카드 0" + (selected + 1), bodyStyle);
            GUILayout.BeginArea(new Rect(panel.x + 26f, panel.y + 118f, width - 52f, height - 190f));
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Label("재료", sectionStyle);
            GUILayout.Label(ingredients[selected], bodyStyle);
            GUILayout.Space(20f);
            GUILayout.Label("만드는 방법", sectionStyle);
            GUILayout.Label(steps[selected], bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.Button(new Rect(panel.x + width - 166f, panel.yMax - 58f, 140f, 40f), "닫기 (Esc)", buttonStyle))
                Close();
        }

        public void Dispose()
        {
            Close();
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Material material in paper)
                if (material != null) UnityEngine.Object.Destroy(material);
            if (ownsFont && font != null) UnityEngine.Object.Destroy(font);
        }
    }

}