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
    private CocktailPractice practice;
    private int practiceReturnFrame = -1;
    public bool IsRecipeOpen => practiceReturnFrame == Time.frameCount || (recipeCards != null && recipeCards.BlocksInput);
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
        if (practice != null && practice.IsActive)
        {
            practice.Tick();
            return;
        }
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
        if (practice != null) practice.Dispose();
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
        if (practice != null && practice.IsActive)
        {
            practice.Draw();
            return;
        }
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

    void BeginPractice(int recipe)
    {
        recipeCards.Close();
        if (practice != null) practice.Dispose();
        practice = new CocktailPractice(this, recipe);
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

    private sealed class CocktailPractice
    {
        private enum Phase { ChooseGlass, Build, Stir, Shake, GlassIce, Transfer, TopUp, Ready }
        private const int Layer = 30;
        private const float RoundSeconds = 45f;
        private const float TableY = 0.98f;
        private readonly BeerGlassPour owner;
        private readonly int recipe;
        private readonly string[] recipeNames = { "Gin & Tonic", "Negroni", "Long Island Iced Tea" };
        private readonly string[] glassNames = { "Rocks glass", "Old-fashioned glass", "Long-drink glass" };
        private readonly string[] ingredientNames = { "Rum", "Gin", "Vodka", "Triple sec", "Campari", "Tonic water", "Ice", "Cola", "Lemon juice", "Sweet vermouth" };
        private readonly int[][] buildOrders = { new[] { 1, 5, 6 }, new[] { 9, 1, 4, 6 }, new[] { 0, 1, 2, 3, 6 } };
        private readonly float[][] buildMeasures = { new[] { 1f, 0f, 0f }, new[] { 1f, 1f, 1f, 0f }, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0f } };
        private readonly float[] glassHeights = { 0.16f, 0.20f, 0.32f };
        private readonly Color ink = new Color(0.89f, 0.94f, 0.92f);
        private readonly Color panelColor = new Color(0.035f, 0.085f, 0.09f, 0.94f);
        private readonly Vector3 origin = new Vector3(80f, 0f, 0f);
        private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
        private readonly System.Collections.Generic.List<Mesh> meshes = new System.Collections.Generic.List<Mesh>();
        private readonly System.Collections.Generic.List<GameObject> iceEffects = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.Dictionary<int, GameObject> targets = new System.Collections.Generic.Dictionary<int, GameObject>();
        private readonly System.Collections.Generic.Dictionary<Collider, int> hitTargets = new System.Collections.Generic.Dictionary<Collider, int>();
        private readonly GameObject[] glassObjects = new GameObject[3];
        private readonly GameObject[] fills = new GameObject[4];
        private readonly GameObject[] rings = new GameObject[3];
        private readonly Material[] ingredientMaterials = new Material[10];
        private readonly float[] levels = new float[4];
        private UnityEngine.SceneManagement.Scene scene;
        private GameObject root, shaker, stirrer, shakerLid, stream;
        private Camera camera;
        private BarPlayerMovement sourceMovement;
        private bool savedMovement, savedUniform, savedCamera;
        private CursorLockMode savedCursor;
        private bool savedCursorVisible;
        private Material steel, glass, ice, liquid, mat, white;
        private Phase phase;
        private int selectedGlass, ingredientStep, strokes, travelDirection, lastStrokeDirection, toolPickedFrame;
        private int hovered = -1;
        private float remaining, measure = 1f, travel, toolOffset;
        private bool failed, completed, toolHeld, pouring, released;
        private string error = "";
        private GameObject pourObject, pourTarget;
        private Vector3 pourStartPosition;
        private Quaternion pourStartRotation;
        private float pourTime, pourBoost;
        private Vector3 stirStart, shakerStart;
        private GUIStyle bodyStyle, titleStyle, smallStyle, labelStyle, buttonStyle, timerStyle;
        public bool IsActive { get; private set; }

        public CocktailPractice(BeerGlassPour owner, int recipe)
        {
            this.owner = owner;
            this.recipe = Mathf.Clamp(recipe, 0, 2);
            sourceMovement = owner.GetComponent<BarPlayerMovement>();
            savedMovement = sourceMovement != null && sourceMovement.enabled;
            savedUniform = owner.uniform != null && owner.uniform.enabled;
            savedCamera = owner.playerCamera.enabled;
            savedCursor = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            scene = UnityEngine.SceneManagement.SceneManager.CreateScene("CocktailPractice_" + System.Guid.NewGuid().ToString("N"));
            root = new GameObject("CocktailPractice_Runtime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = origin;
            root.layer = Layer;
            BuildScene();
            owner.playerCamera.enabled = false;
            if (sourceMovement != null) sourceMovement.enabled = false;
            if (owner.uniform != null) owner.uniform.enabled = false;
            camera.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsActive = true;
            ResetRound();
        }

        private Material MakeMaterial(Color color, bool metallic = false, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", metallic ? 0.78f : 0.4f);
            if (metallic) material.SetFloat("_Metallic", 0.85f);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            materials.Add(material);
            return material;
        }

        private GameObject NewObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.layer = Layer;
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.layer = Layer;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            return obj;
        }

        private void Register(GameObject obj, int code, Vector3 center, Vector3 size)
        {
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            targets.Add(code, obj);
            hitTargets.Add(collider, code);
        }

        private void Cup(Transform parent, float radius, float height, Material material, int segments = 48)
        {
            Vector2[] profile = { new Vector2(0f, 0f), new Vector2(radius * 0.82f, 0f),
                new Vector2(radius, height), new Vector2(radius - 0.003f, height),
                new Vector2(radius * 0.82f - 0.003f, 0.007f), new Vector2(0f, 0.007f), new Vector2(0f, 0f) };
            Vector3[] vertices = new Vector3[profile.Length * (segments + 1)];
            int[] triangles = new int[(profile.Length - 1) * segments * 6];
            int cursor = 0;
            for (int row = 0; row < profile.Length; row++)
                for (int column = 0; column <= segments; column++)
                {
                    float angle = column * Mathf.PI * 2f / segments;
                    int index = row * (segments + 1) + column;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * profile[row].x, profile[row].y, Mathf.Sin(angle) * profile[row].x);
                    if (row == profile.Length - 1 || column == segments) continue;
                    int next = index + segments + 1;
                    triangles[cursor++] = index; triangles[cursor++] = next; triangles[cursor++] = next + 1;
                    triangles[cursor++] = index; triangles[cursor++] = next + 1; triangles[cursor++] = index + 1;
                }
            Mesh mesh = new Mesh { name = "PracticeHollowCup" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            GameObject surface = NewObject("HollowCup", parent);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void BuildScene()
        {
            mat = MakeMaterial(new Color(0.08f, 0.16f, 0.17f));
            steel = MakeMaterial(new Color(0.68f, 0.75f, 0.78f), true);
            glass = MakeMaterial(new Color(0.7f, 0.88f, 0.94f, 0.24f), false, true);
            ice = MakeMaterial(new Color(0.75f, 0.92f, 1f, 0.78f), false, true);
            white = MakeMaterial(new Color(0.85f, 0.91f, 0.85f));
            Color[] drinkColors = { new Color(0.75f, 0.91f, 0.8f), new Color(0.76f, 0.13f, 0.08f), new Color(0.65f, 0.32f, 0.08f) };
            liquid = MakeMaterial(drinkColors[recipe]);
            Part("Floor", PrimitiveType.Cube, root.transform, new Vector3(0f, -0.05f, 1f), new Vector3(8f, 0.1f, 7f), mat);
            Part("Backdrop", PrimitiveType.Cube, root.transform, new Vector3(0f, 2f, 2.7f), new Vector3(7f, 4f, 0.1f), mat);
            Part("Bench", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.45f, 0.8f), new Vector3(4.4f, 0.9f, 1.7f), mat);
            Part("Worktop", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.94f, 0.8f), new Vector3(4.6f, 0.08f, 1.9f), steel);
            Part("BarMat", PrimitiveType.Cube, root.transform, new Vector3(0f, TableY + 0.003f, 0.35f), new Vector3(4.2f, 0.006f, 0.8f), mat);
            GameObject view = NewObject("PracticeFirstPersonCamera", root.transform);
            view.transform.localPosition = new Vector3(0f, 1.8f, -1.6f);
            view.transform.LookAt(origin + new Vector3(0f, 1.15f, 0.9f));
            camera = view.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.065f, 0.075f);
            GameObject lighting = NewObject("PracticeKeyLight", root.transform);
            lighting.transform.localRotation = Quaternion.Euler(40f, -25f, 0f);
            Light light = lighting.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f; light.cullingMask = 1 << Layer;
            Color[] colors = { new Color(0.5f, 0.22f, 0.08f), new Color(0.22f, 0.52f, 0.36f), new Color(0.55f, 0.73f, 0.85f),
                new Color(0.9f, 0.51f, 0.12f), new Color(0.8f, 0.08f, 0.13f), new Color(0.35f, 0.73f, 0.82f),
                new Color(0.7f, 0.9f, 1f), new Color(0.19f, 0.09f, 0.06f), new Color(0.95f, 0.81f, 0.23f), new Color(0.42f, 0.1f, 0.2f) };
            for (int index = 0; index < ingredientNames.Length; index++)
            {
                ingredientMaterials[index] = MakeMaterial(colors[index]);
                GameObject bottle = NewObject(ingredientNames[index], root.transform);
                bottle.transform.localPosition = new Vector3(-1.8f + index * 0.4f, TableY + 0.006f, 1.44f);
                if (index == 6)
                {
                    Cup(bottle.transform, 0.13f, 0.075f, steel);
                    for (int cube = 0; cube < 6; cube++)
                        Part("IceCube", PrimitiveType.Cube, bottle.transform, new Vector3((cube % 3 - 1) * 0.055f,
                            0.06f + (cube / 3) * 0.025f, (cube / 3 - 0.5f) * 0.06f), Vector3.one * 0.05f, ice);
                    Register(bottle, 10 + index, Vector3.up * 0.08f, new Vector3(0.27f, 0.16f, 0.27f));
                }
                else
                {
                    Part("BottleBody", PrimitiveType.Cylinder, bottle.transform, Vector3.up * 0.145f, new Vector3(0.14f, 0.145f, 0.14f), ingredientMaterials[index]);
                    Part("Shoulder", PrimitiveType.Sphere, bottle.transform, Vector3.up * 0.29f, new Vector3(0.14f, 0.08f, 0.14f), ingredientMaterials[index]);
                    Part("Neck", PrimitiveType.Cylinder, bottle.transform, Vector3.up * 0.35f, new Vector3(0.055f, 0.055f, 0.055f), ingredientMaterials[index]);
                    Part("Cap", PrimitiveType.Cylinder, bottle.transform, Vector3.up * 0.414f, new Vector3(0.064f, 0.012f, 0.064f), steel);
                    Part("Label", PrimitiveType.Cylinder, bottle.transform, Vector3.up * 0.16f, new Vector3(0.143f, 0.047f, 0.143f), white);
                    Register(bottle, 10 + index, Vector3.up * 0.22f, new Vector3(0.18f, 0.44f, 0.18f));
                }
            }
            Material highlight = MakeMaterial(new Color(0.25f, 0.8f, 0.53f));
            for (int index = 0; index < 3; index++)
            {
                GameObject vessel = NewObject(glassNames[index], root.transform);
                vessel.transform.localPosition = new Vector3(-1.2f + index * 0.55f, TableY + 0.006f, 0.32f);
                float radius = index == 0 ? 0.07f : index == 1 ? 0.083f : 0.055f;
                Cup(vessel.transform, radius, glassHeights[index], glass, index == 1 ? 12 : 48);
                Register(vessel, index, Vector3.up * glassHeights[index] * 0.5f, new Vector3(radius * 2f, glassHeights[index], radius * 2f));
                glassObjects[index] = vessel;
                fills[index] = Part("Drink", PrimitiveType.Cylinder, vessel.transform, Vector3.up * 0.01f, new Vector3(radius * 1.5f, 0.001f, radius * 1.5f), liquid);
                rings[index] = Part("SelectedGlass", PrimitiveType.Cylinder, root.transform,
                    vessel.transform.localPosition - Vector3.up * 0.002f, new Vector3(radius * 2.5f, 0.002f, radius * 2.5f), highlight);
            }
            shaker = NewObject("Shaker", root.transform);
            shaker.transform.localPosition = new Vector3(0.6f, TableY + 0.006f, 0.35f);
            shakerStart = shaker.transform.localPosition;
            Cup(shaker.transform, 0.065f, 0.28f, steel);
            Register(shaker, 31, Vector3.up * 0.15f, new Vector3(0.15f, 0.33f, 0.15f));
            shakerLid = Part("ShakerLid", PrimitiveType.Cylinder, shaker.transform, Vector3.up * 0.30f, new Vector3(0.11f, 0.04f, 0.11f), steel);
            fills[3] = Part("ShakerMixture", PrimitiveType.Cylinder, shaker.transform, Vector3.up * 0.01f, new Vector3(0.095f, 0.001f, 0.095f), liquid);
            stirrer = NewObject("Stirring stick", root.transform);
            stirrer.transform.localPosition = new Vector3(1.4f, TableY + 0.006f, 0.30f);
            stirStart = stirrer.transform.localPosition;
            Part("SpoonStem", PrimitiveType.Cylinder, stirrer.transform, Vector3.up * 0.18f, new Vector3(0.009f, 0.16f, 0.009f), steel);
            Part("SpoonBowl", PrimitiveType.Sphere, stirrer.transform, Vector3.up * 0.022f, new Vector3(0.03f, 0.025f, 0.045f), steel);
            Register(stirrer, 30, Vector3.up * 0.18f, new Vector3(0.12f, 0.36f, 0.12f));
            GameObject jigger = NewObject("DoubleJigger", root.transform);
            jigger.transform.localPosition = new Vector3(0.06f, TableY + 0.006f, -0.05f);
            GameObject upper = NewObject("1oz", jigger.transform);
            upper.transform.localPosition = Vector3.up * 0.05f; Cup(upper.transform, 0.036f, 0.065f, steel);
            GameObject lower = NewObject("0.5oz", jigger.transform);
            lower.transform.localPosition = Vector3.up * 0.05f; lower.transform.localRotation = Quaternion.Euler(180f, 0f, 0f); Cup(lower.transform, 0.028f, 0.05f, steel);
            stream = Part("PourStream", PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(0.014f, 0.12f, 0.014f), liquid);
            stream.SetActive(false);
            Physics.SyncTransforms();
        }

        private void ResetRound()
        {
            StopPour(false);
            foreach (GameObject effect in iceEffects) if (effect != null) UnityEngine.Object.Destroy(effect);
            iceEffects.Clear();
            for (int index = 0; index < fills.Length; index++) { levels[index] = 0f; fills[index].SetActive(false); }
            foreach (GameObject ring in rings) ring.SetActive(false);
            ReturnTool();
            shakerLid.SetActive(false);
            phase = Phase.ChooseGlass; selectedGlass = -1; ingredientStep = 0;
            strokes = 0; travel = 0f; travelDirection = 0; lastStrokeDirection = 0;
            failed = false; completed = false; error = ""; measure = 1f; remaining = RoundSeconds;
            hovered = -1;
        }

        private int ExpectedIngredient()
        {
            if (phase == Phase.Build) return buildOrders[recipe][ingredientStep];
            if (phase == Phase.GlassIce) return 6;
            if (phase == Phase.TopUp) return ingredientStep == 0 ? 8 : 7;
            return -1;
        }

        private float ExpectedMeasure()
        {
            if (phase == Phase.Build) return buildMeasures[recipe][ingredientStep];
            return phase == Phase.TopUp && ingredientStep == 0 ? 1f : 0f;
        }

        private string NextInstruction()
        {
            switch (phase)
            {
                case Phase.ChooseGlass: return "Choose the " + glassNames[recipe] + ".";
                case Phase.Stir: return toolHeld ? "Hold LEFT CLICK and move LEFT / RIGHT. Complete 15 round trips." : "Click the stirring stick to pick it up.";
                case Phase.Shake: return toolHeld ? "Hold LEFT CLICK and move UP / DOWN. Complete 15 round trips." : "Click the shaker to pick it up.";
                case Phase.Transfer: return "Click the shaker to pour into the iced long-drink glass.";
                case Phase.Ready: return "Your cocktail is ready. Click FINISH.";
                default:
                    int ingredient = ExpectedIngredient();
                    string amount = ExpectedMeasure() > 0f ? ExpectedMeasure().ToString("0.#") + " oz of " : "";
                    return "Click " + amount + ingredientNames[ingredient] + (phase == Phase.Build && recipe == 2 ? " to add it to the shaker." : " to add it to your glass.");
            }
        }

        private void Fail(string message)
        {
            error = message; failed = true; StopPour(false); ReturnTool();
        }

        private void AdvanceIngredient()
        {
            if (phase == Phase.GlassIce) { phase = Phase.Transfer; return; }
            if (phase == Phase.TopUp)
            {
                ingredientStep++;
                if (ingredientStep == 2) phase = Phase.Ready;
                return;
            }
            ingredientStep++;
            if (ingredientStep == buildOrders[recipe].Length)
            {
                phase = recipe == 0 ? Phase.Ready : recipe == 1 ? Phase.Stir : Phase.Shake;
                if (phase == Phase.Shake) shakerLid.SetActive(true);
            }
        }

        private void AddIngredient(int ingredient)
        {
            if (phase == Phase.ChooseGlass) { Fail("Choose the correct glass before adding ingredients."); return; }
            int expected = ExpectedIngredient();
            if (expected < 0) { Fail("That ingredient is not allowed at this step.\n" + NextInstruction()); return; }
            if (ingredient != expected)
            {
                Fail("Incorrect ingredient or order.\nExpected: " + ingredientNames[expected] + ".\nYou chose: " + ingredientNames[ingredient] + "."); return;
            }
            float required = ExpectedMeasure();
            if (required > 0f && !Mathf.Approximately(measure, required))
            {
                Fail("Incorrect measure.\nUse " + required.ToString("0.#") + " oz for " + ingredientNames[ingredient] + "."); return;
            }
            GameObject vessel = phase == Phase.Build && recipe == 2 ? shaker : glassObjects[selectedGlass];
            if (ingredient == 6) AddIce(vessel);
            else BeginPour(targets[10 + ingredient], vessel, ingredientMaterials[ingredient], ingredient == 5 || ingredient == 7 ? 0.07f : 0.028f);
            AdvanceIngredient();
        }

        private void AddIce(GameObject vessel)
        {
            for (int index = 0; index < 4; index++)
            {
                GameObject cube = Part("AddedIce", PrimitiveType.Cube, vessel.transform,
                    new Vector3((index % 2 - 0.5f) * 0.04f, 0.06f + index / 2 * 0.035f, (index / 2 - 0.5f) * 0.035f), Vector3.one * 0.033f, ice);
                cube.transform.localRotation = Quaternion.Euler(12f * index, 23f * index, 9f * index);
                iceEffects.Add(cube);
            }
        }

        private void BeginPour(GameObject bottle, GameObject vessel, Material material, float boost)
        {
            pouring = true; pourTime = 0f; pourObject = bottle; pourTarget = vessel; pourBoost = boost;
            pourStartPosition = bottle.transform.localPosition; pourStartRotation = bottle.transform.localRotation;
            stream.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void TickPour()
        {
            pourTime += Time.unscaledDeltaTime;
            float time = Mathf.Clamp01(pourTime / 0.45f);
            float blend = time < 0.25f ? time * 4f : time > 0.75f ? (1f - time) * 4f : 1f;
            Quaternion tilted = Quaternion.Euler(0f, 0f, -55f);
            Vector3 spout = tilted * (Vector3.up * (pourObject == shaker ? 0.3f : 0.42f));
            Vector3 destination = pourTarget.transform.localPosition + Vector3.up * 0.35f - spout;
            pourObject.transform.localPosition = Vector3.Lerp(pourStartPosition, destination, blend);
            pourObject.transform.localRotation = Quaternion.Slerp(pourStartRotation, tilted, blend);
            stream.transform.localPosition = pourTarget.transform.localPosition + Vector3.up * 0.22f;
            stream.SetActive(time > 0.25f && time < 0.75f);
            if (time >= 1f) StopPour(true);
        }

        private void StopPour(bool apply)
        {
            if (!pouring) return;
            if (pourObject != null) { pourObject.transform.localPosition = pourStartPosition; pourObject.transform.localRotation = pourStartRotation; }
            if (stream != null) stream.SetActive(false);
            if (apply && pourTarget != null)
            {
                int index = pourTarget == shaker ? 3 : selectedGlass;
                float cap = index == 3 ? 0.20f : glassHeights[index] * 0.73f;
                levels[index] = Mathf.Min(cap, levels[index] + pourBoost);
                GameObject fill = fills[index];
                fill.SetActive(true);
                fill.transform.localPosition = Vector3.up * (0.008f + levels[index] * 0.5f);
                Vector3 scale = fill.transform.localScale; scale.y = levels[index] * 0.5f; fill.transform.localScale = scale;
            }
            if (apply && pourObject == shaker) { fills[3].SetActive(false); levels[3] = 0f; }
            pouring = false; pourObject = null; pourTarget = null;
        }

        private void SelectTarget(int code)
        {
            if (code >= 10 && code < 20) { AddIngredient(code - 10); return; }
            if (code >= 0 && code < 3)
            {
                if (phase != Phase.ChooseGlass) return;
                if (code != recipe) { Fail("Incorrect glass.\nChoose the " + glassNames[recipe] + " for " + recipeNames[recipe] + "."); return; }
                selectedGlass = code; rings[code].SetActive(true); phase = Phase.Build; return;
            }
            if (phase == Phase.Transfer && code == 31)
            {
                shakerLid.SetActive(false);
                BeginPour(shaker, glassObjects[selectedGlass], liquid, 0.15f);
                phase = Phase.TopUp; ingredientStep = 0; return;
            }
            if ((phase == Phase.Stir && code == 30) || (phase == Phase.Shake && code == 31))
            {
                toolHeld = true; toolPickedFrame = Time.frameCount; travel = 0f; travelDirection = 0; toolOffset = 0f;
                if (phase == Phase.Stir)
                    stirrer.transform.position = glassObjects[selectedGlass].transform.position + Vector3.up * 0.08f;
                else
                {
                    shaker.transform.localPosition = new Vector3(0.3f, 1.28f, -0.72f);
                    shaker.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
                }
            }
        }

        private void ReturnTool()
        {
            toolHeld = false; toolOffset = 0f;
            if (stirrer != null) { stirrer.transform.localPosition = stirStart; stirrer.transform.localRotation = Quaternion.identity; }
            if (shaker != null) { shaker.transform.localPosition = shakerStart; shaker.transform.localRotation = Quaternion.identity; }
        }

        private void CountMotion(float delta)
        {
            if (Mathf.Abs(delta) < 0.5f) return;
            int direction = delta > 0f ? 1 : -1;
            if (direction != travelDirection) { travelDirection = direction; travel = 0f; }
            travel += Mathf.Abs(delta);
            float threshold = Mathf.Clamp(Screen.height * 0.05f, 20f, 60f);
            if (travel < threshold || direction == lastStrokeDirection) return;
            lastStrokeDirection = direction; travel = 0f; strokes++;
            if (strokes >= 30)
            {
                ReturnTool();
                phase = phase == Phase.Stir ? Phase.Ready : Phase.GlassIce;
            }
        }

        private Vector2 Pointer()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
        private Vector2 PointerDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 18f;
#endif
        }
        private bool LeftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }
        private Rect MeasurePanel() { return new Rect(16f, Screen.height - 118f, 302f, 102f); }
        private Rect FinishButton() { return new Rect(Screen.width - 202f, Screen.height - 83f, 186f, 62f); }
        private bool OverUI(Vector2 pointer)
        {
            return new Rect(0f, 0f, Screen.width, 132f).Contains(pointer) || MeasurePanel().Contains(pointer)
                || (phase == Phase.Ready && FinishButton().Contains(pointer));
        }
        private Rect LabelRect(int code)
        {
            if (code >= 10 && code < 20)
            {
                int index = code - 10;
                float cellWidth = (Screen.width - 32f) / 5f;
                return new Rect(16f + index % 5 * cellWidth, 144f + index / 5 * 40f, cellWidth - 6f, 36f);
            }
            GameObject target = targets[code];
            float height = code >= 10 && code < 20 ? (code == 16 ? 0.17f : 0.49f) : -0.03f;
            Vector3 screen = camera.WorldToScreenPoint(target.transform.position + Vector3.up * height);
            const float labelWidth = 136f;
            float x = Mathf.Clamp(screen.x - labelWidth * 0.5f, 8f, Mathf.Max(8f, Screen.width - labelWidth - 8f));
            return new Rect(x, Screen.height - screen.y - 21f, labelWidth, 42f);
        }
        private string TargetName(int code)
        {
            return code < 3 ? glassNames[code] : code < 20 ? ingredientNames[code - 10] : code == 30 ? "Stirring stick" : "Shaker";
        }
        public void Tick()
        {
            if (!IsActive) return;
            if (owner.PressedRecipeClose()) { Exit(); return; }
            if (failed || completed) return;
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            if (remaining <= 0f) { Fail("Time's up!\nThe 45-second limit has expired."); return; }
            if (pouring) { TickPour(); return; }
            Vector2 pointer = Pointer(); Vector2 guiPointer = new Vector2(pointer.x, Screen.height - pointer.y);
            if (toolHeld)
            {
                if (LeftHeld() && !OverUI(guiPointer) && toolPickedFrame != Time.frameCount)
                {
                    Vector2 delta = PointerDelta();
                    toolOffset = Mathf.Clamp(toolOffset + (phase == Phase.Stir ? delta.x : delta.y) * 0.002f, -0.12f, 0.12f);
                    if (phase == Phase.Stir)
                        stirrer.transform.position = glassObjects[selectedGlass].transform.position + new Vector3(toolOffset * 0.25f, 0.08f, 0f);
                    else
                    {
                        shaker.transform.localPosition = new Vector3(0.3f, 1.28f + toolOffset, -0.72f);
                        shaker.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
                    }
                    CountMotion(phase == Phase.Stir ? delta.x : delta.y);
                }
                else { travel = 0f; travelDirection = 0; }
                return;
            }
            hovered = -1;
            if (OverUI(guiPointer)) return;
            foreach (int code in targets.Keys)
                if (LabelRect(code).Contains(guiPointer)) { hovered = code; break; }
            if (hovered < 0 && Physics.Raycast(camera.ScreenPointToRay(pointer), out RaycastHit hit, 8f,
                1 << Layer, QueryTriggerInteraction.Ignore) && hitTargets.TryGetValue(hit.collider, out int found)) hovered = found;
            if (hovered >= 0 && owner.PressedLeftClick()) SelectTarget(hovered);
        }

        private void EnsureStyles()
        {
            if (bodyStyle != null) return;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            bodyStyle.normal.textColor = ink;
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 25, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 13 };
            labelStyle = new GUIStyle(bodyStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            timerStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleRight };
        }
        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        private void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            if (camera != null) camera.enabled = false;
            if (owner.playerCamera != null) owner.playerCamera.enabled = savedCamera;
            if (sourceMovement != null) sourceMovement.enabled = savedMovement;
            if (owner.uniform != null) owner.uniform.enabled = savedUniform;
            Cursor.lockState = savedCursor; Cursor.visible = savedCursorVisible;
            owner.practiceReturnFrame = Time.frameCount;
            ReleaseResources();
        }
        private void ReleaseResources()
        {
            if (released) return;
            released = true;
            if (root != null) root.SetActive(false);
            if (scene.IsValid() && scene.isLoaded && Application.isPlaying) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            foreach (Material material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (Mesh mesh in meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
        }
        public void Draw()
        {
            if (!IsActive) return;
            EnsureStyles();
            if (failed || completed)
            {
                Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.76f));
                float width = Mathf.Min(600f, Screen.width - 32f);
                Rect popup = new Rect((Screen.width - width) * 0.5f, (Screen.height - 300f) * 0.5f, width, 300f);
                Fill(popup, panelColor);
                GUI.Label(new Rect(popup.x + 26f, popup.y + 20f, width - 52f, 50f), completed ? "COCKTAIL COMPLETE" : "TRY AGAIN", titleStyle);
                GUI.Label(new Rect(popup.x + 26f, popup.y + 80f, width - 52f, 120f), completed
                    ? recipeNames[recipe] + " is ready to serve.\nCorrect glass, ingredients and method!" : error, bodyStyle);
                if (GUI.Button(new Rect(popup.x + 26f, popup.yMax - 70f, (width - 68f) * 0.5f, 44f), "Retry - 45 seconds", buttonStyle)) ResetRound();
                if (GUI.Button(new Rect(popup.center.x + 8f, popup.yMax - 70f, (width - 68f) * 0.5f, 44f), "Back to the bar", buttonStyle)) Exit();
                return;
            }
            Fill(new Rect(16f, 16f, Screen.width - 32f, 116f), panelColor);
            GUI.Label(new Rect(34f, 26f, Screen.width - 340f, 36f), recipeNames[recipe] + " / PRACTICE", titleStyle);
            GUI.Label(new Rect(34f, 68f, Screen.width - 290f, 48f), pouring ? "Pouring..." : NextInstruction(), bodyStyle);
            Color old = GUI.color; GUI.color = remaining <= 10f ? new Color(1f, 0.4f, 0.3f) : ink;
            GUI.Label(new Rect(Screen.width - 235f, 65f, 195f, 38f), remaining.ToString("0.0") + " s / 45", timerStyle); GUI.color = old;
            Fill(new Rect(34f, 120f, (Screen.width - 68f) * remaining / RoundSeconds, 3f), remaining <= 10f ? Color.red : new Color(0.4f, 0.8f, 0.6f));
            if (GUI.Button(new Rect(Screen.width - 116f, 28f, 82f, 28f), "Exit [Esc]", buttonStyle)) { Exit(); return; }
            foreach (int code in targets.Keys)
            {
                Rect rect = LabelRect(code);
                bool expected = (phase == Phase.ChooseGlass && code == recipe) || (ExpectedIngredient() >= 0 && code == 10 + ExpectedIngredient())
                    || (phase == Phase.Stir && code == 30) || ((phase == Phase.Shake || phase == Phase.Transfer) && code == 31);
                Fill(rect, code == hovered ? new Color(0.2f, 0.4f, 0.32f, 0.95f) : new Color(0.03f, 0.08f, 0.08f, 0.86f));
                if (code >= 10 && code < 20) Fill(new Rect(rect.x, rect.y, 3f, rect.height), ingredientMaterials[code - 10].color);
                Color previous = GUI.color; if (expected) GUI.color = new Color(0.6f, 1f, 0.72f);
                GUI.Label(rect, TargetName(code), labelStyle); GUI.color = previous;
            }
            Rect measures = MeasurePanel(); Fill(measures, panelColor);
            GUI.Label(new Rect(measures.x + 12f, measures.y + 7f, 278f, 26f), "JIGGER MEASURE", smallStyle);
            bool canMeasure = !pouring && !toolHeld;
            bool previousEnabled = GUI.enabled; GUI.enabled = canMeasure;
            if (GUI.Button(new Rect(measures.x + 12f, measures.y + 38f, 132f, 48f), (measure == 0.5f ? "SELECTED\n" : "") + "0.5 oz", buttonStyle)) measure = 0.5f;
            if (GUI.Button(new Rect(measures.x + 158f, measures.y + 38f, 132f, 48f), (measure == 1f ? "SELECTED\n" : "") + "1 oz", buttonStyle)) measure = 1f;
            GUI.enabled = previousEnabled;
            if (phase == Phase.Stir || phase == Phase.Shake)
            {
                Rect progress = new Rect(334f, Screen.height - 110f, Mathf.Max(200f, Screen.width - 350f), 94f); Fill(progress, panelColor);
                GUI.Label(new Rect(progress.x + 12f, progress.y + 8f, progress.width - 24f, 32f),
                    (phase == Phase.Stir ? "STIR LEFT / RIGHT: " : "SHAKE UP / DOWN: ") + (strokes / 2) + " / 15 ROUND TRIPS", bodyStyle);
                GUI.Label(new Rect(progress.x + 12f, progress.y + 42f, progress.width - 24f, 42f),
                    toolHeld ? "Hold LEFT CLICK and drag. Tiny movements do not count." : "Click the highlighted tool, then hold LEFT CLICK and drag.", smallStyle);
            }
            if (phase != Phase.Stir && phase != Phase.Shake && phase != Phase.Ready)
                GUI.Label(new Rect(334f, Screen.height - 102f, Mathf.Max(200f, Screen.width - 350f), 82f),
                    "Click a glass, then choose a jigger measure.\nClick a bottle or its ingredient label to add it.\nESC returns to the bar.", smallStyle);
            if (phase == Phase.Ready && !pouring && GUI.Button(FinishButton(), "FINISH", buttonStyle)) completed = true;
        }
        public void Dispose()
        {
            Exit();
            ReleaseResources();
        }
    }

    private sealed class RecipeCards
    {
        private readonly BeerGlassPour owner;
        private GameObject root;
        private readonly GameObject[] notes = new GameObject[3];
        private readonly Material[] paper = new Material[3];
        private readonly Material[] artMaterials = new Material[3];
        private readonly string[] titles = { "Gin & Tonic", "Negroni", "Long Island Iced Tea" };
        private readonly string[] shortTitles = { "Gin & Tonic", "Negroni", "Long Island" };
        private readonly string[] noteTitles = { "GIN & TONIC", "NEGRONI", "LONG ISLAND\nICED TEA" };
        private readonly string[] methods = { "BUILD IN GLASS", "STIR IN GLASS", "SHAKE & TOP" };
        private readonly string[] glasses = { "Rocks glass", "Old-fashioned glass", "Long-drink glass" };
        private readonly string[] descriptions = { "Gin, tonic and ice. Keep it simple.", "Equal parts. Stirred over ice.", "Four spirits. Shake, then top." };
        private readonly string[][] ingredientNames = {
            new[] { "Gin", "Tonic water", "Ice" },
            new[] { "Sweet vermouth", "Gin", "Campari", "Ice" },
            new[] { "Rum", "Gin", "Vodka", "Triple sec", "Lemon juice", "Cola", "Ice" }
        };
        private readonly string[][] amounts = {
            new[] { "1 oz", "Top up", "As needed" },
            new[] { "1 oz", "1 oz", "1 oz", "As needed" },
            new[] { "0.5 oz", "0.5 oz", "0.5 oz", "0.5 oz", "1 oz", "Top up", "As needed" }
        };
        private readonly string[][] steps = {
            new[] { "Choose a rocks glass.", "Measure 1 oz of gin accurately and pour it into the glass.", "Fill with tonic water.", "Add ice and serve." },
            new[] { "Choose an old-fashioned glass.", "Measure and pour 1 oz each of sweet vermouth, gin and Campari.", "Add ice and stir thoroughly." },
            new[] { "Prepare a long-drink glass.", "Measure 0.5 oz each of rum, gin, vodka and triple sec into the shaker.", "Add ice and shake thoroughly.", "Pour into the long-drink glass filled with ice.", "Add 1 oz of lemon juice on top.", "Top up with cola." }
        };
        private readonly Color ink = new Color(0.13f, 0.20f, 0.20f);
        private readonly Color muted = new Color(0.40f, 0.43f, 0.39f);
        private readonly Color cream = new Color(246f / 255f, 239f / 255f, 228f / 255f);
        private readonly Color[] accents = { new Color(0.27f, 0.43f, 0.34f), new Color(0.65f, 0.29f, 0.22f), new Color(0.65f, 0.45f, 0.16f) };
        private readonly System.Collections.Generic.List<Font> ownedFonts = new System.Collections.Generic.List<Font>();
        private Font font;
        private Font displayFont;
        private Texture2D illustration;
        private int selected = -1;
        private int hovered = -1;
        private int closeFrame = -1;
        private Vector2 scroll;
        private CursorLockMode previousLock;
        private bool previousCursorVisible;
        private GUIStyle titleStyle, sectionStyle, bodyStyle, buttonStyle, smallStyle, amountStyle, numberStyle, centeredStyle;
        public bool IsOpen => selected >= 0;
        public bool BlocksInput => IsOpen || closeFrame == Time.frameCount;

        public RecipeCards(BeerGlassPour owner, BoxCollider table)
        {
            this.owner = owner;
            if (table == null) return;
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = ChooseFont(new[] { "Helvetica Neue", "Arial", "Liberation Sans" }, fallback, 20);
            displayFont = ChooseFont(new[] { "Georgia", "Times New Roman", "Liberation Serif" }, font, 38);
            illustration = new Texture2D(2, 2, TextureFormat.RGB24, false);
            illustration.LoadImage(System.Convert.FromBase64String(RecipeIllustrationBase64), true);
            illustration.filterMode = FilterMode.Bilinear;
            illustration.wrapMode = TextureWrapMode.Clamp;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;
            root = new GameObject("CocktailRecipeNotes_Runtime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            root.transform.position = table.transform.TransformPoint(table.center
                + Vector3.up * table.size.y * 0.5f) + Vector3.up * 0.004f;
            root.transform.rotation = table.transform.rotation;
            for (int index = 0; index < 3; index++)
            {
                GameObject note = GameObject.CreatePrimitive(PrimitiveType.Cube);
                note.name = "RecipeNote_" + (index + 1);
                note.transform.SetParent(root.transform, false);
                note.transform.localPosition = new Vector3(-1.9f + index * 0.43f, 0f, 0f);
                note.transform.localScale = new Vector3(0.30f, 0.003f, 0.36f);
                paper[index] = new Material(shader) { color = cream };
                note.GetComponent<Renderer>().sharedMaterial = paper[index];
                notes[index] = note;
                AddNoteText(index, "0" + (index + 1) + "  " + noteTitles[index],
                    new Vector3(-0.13f, 0.003f, 0.15f), 0.016f);
                AddNoteText(index, "RECIPE CARD  /  CLICK TO READ",
                    new Vector3(-0.13f, 0.003f, -0.145f), 0.010f);
                GameObject art = GameObject.CreatePrimitive(PrimitiveType.Quad);
                art.name = "CocktailIllustration";
                art.transform.SetParent(root.transform, false);
                art.transform.localPosition = note.transform.localPosition + new Vector3(0f, 0.0035f, -0.015f);
                art.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                art.transform.localScale = new Vector3(0.12f, 0.24f, 1f);
                Collider collider = art.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
                artMaterials[index] = new Material(shader) { color = Color.white };
                artMaterials[index].mainTexture = illustration;
                artMaterials[index].mainTextureScale = new Vector2(1f / 3f, 1f);
                artMaterials[index].mainTextureOffset = new Vector2(index / 3f, 0f);
                art.GetComponent<Renderer>().sharedMaterial = artMaterials[index];
            }
            root.SetActive(false);
        }

        private Font ChooseFont(string[] choices, Font fallback, int size)
        {
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string name in choices)
            {
                if (!System.Array.Exists(installed, candidate => string.Equals(candidate, name, System.StringComparison.OrdinalIgnoreCase))) continue;
                Font chosen = Font.CreateDynamicFontFromOSFont(name, size);
                if (chosen == null) continue;
                ownedFonts.Add(chosen);
                return chosen;
            }
            return fallback;
        }

        private void AddNoteText(int index, string content, Vector3 offset, float size)
        {
            GameObject writing = new GameObject("PrintedRecipeLabel");
            writing.transform.SetParent(root.transform, false);
            writing.transform.localPosition = notes[index].transform.localPosition + offset;
            writing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh text = writing.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 48;
            text.characterSize = size;
            text.anchor = TextAnchor.UpperLeft;
            text.color = ink;
            text.text = content;
            writing.GetComponent<Renderer>().sharedMaterial = font.material;
        }

        public void SetAvailable(bool available)
        {
            if (root != null && root.activeSelf != available) root.SetActive(available);
        }

        public bool HandleInput(bool clicked, out string prompt)
        {
            prompt = "";
            hovered = -1;
            if (root == null || !root.activeInHierarchy || BlocksInput) return false;
            Ray ray = owner.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, owner.pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            for (int index = 0; index < notes.Length; index++)
            {
                if (hit.collider.gameObject != notes[index]) continue;
                hovered = index;
                prompt = "LEFT CLICK - Read " + titles[index];
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
            if (root == null || !root.activeInHierarchy || hovered < 0) return;
            Vector3 screen = owner.playerCamera.WorldToScreenPoint(notes[hovered].transform.position + Vector3.up * 0.08f);
            if (screen.z > 0f)
                GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 35f, 200f, 32f), "READ RECIPE 0" + (hovered + 1), style);
        }

        private GUIStyle TextStyle(int size, Font face, Color color, FontStyle weight = FontStyle.Normal)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { font = face, fontSize = size, fontStyle = weight,
                wordWrap = true, padding = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = color;
            return style;
        }

        private void EnsureStyles()
        {
            if (bodyStyle != null) return;
            bodyStyle = TextStyle(18, font, ink);
            titleStyle = TextStyle(38, displayFont, ink);
            sectionStyle = TextStyle(12, font, muted, FontStyle.Bold);
            smallStyle = TextStyle(13, font, muted);
            amountStyle = TextStyle(17, font, ink, FontStyle.Bold);
            amountStyle.alignment = TextAnchor.UpperRight;
            numberStyle = TextStyle(16, font, muted, FontStyle.Bold);
            centeredStyle = TextStyle(13, font, muted);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle = TextStyle(14, font, ink, FontStyle.Bold);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.hover.textColor = new Color(0.25f, 0.5f, 0.36f);
            buttonStyle.active.textColor = buttonStyle.hover.textColor;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private bool Button(Rect rect, string label, bool active = false)
        {
            Fill(rect, active ? new Color(0.87f, 0.88f, 0.81f) : new Color(1f, 1f, 1f, 0.32f));
            return GUI.Button(rect, label, buttonStyle);
        }

        private void DrawIllustration(Rect space)
        {
            if (illustration == null) return;
            float aspect = illustration.width / (3f * illustration.height);
            float width = Mathf.Min(space.width, space.height * aspect);
            float height = width / aspect;
            Rect picture = new Rect(space.center.x - width * 0.5f, space.center.y - height * 0.5f, width, height);
            GUI.DrawTextureWithTexCoords(picture, illustration, new Rect(selected / 3f, 0f, 1f / 3f, 1f));
        }

        private void SwitchTo(int index)
        {
            selected = (index + 3) % 3;
            scroll = Vector2.zero;
            // Rebuild GUILayout before drawing a different number of recipe rows.
            GUIUtility.ExitGUI();
        }

        public void DrawOverlay()
        {
            if (!IsOpen) return;
            EnsureStyles();
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.025f, 0.055f, 0.055f, 0.86f));
            const float width = 1040f;
            const float height = 680f;
            float scale = Mathf.Min((Screen.width - 32f) / width, (Screen.height - 32f) / height, 1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * 0.5f,
                (Screen.height - height * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            try
            {
                Fill(new Rect(8f, 12f, width, height), new Color(0f, 0f, 0f, 0.2f));
                Fill(new Rect(0f, 0f, width, height), cream);
                Fill(new Rect(0f, 0f, width, 5f), accents[selected]);
                GUI.Label(new Rect(36f, 24f, 300f, 22f), "THE  BAR  BOOK", sectionStyle);
                GUI.Label(new Rect(36f, 55f, 290f, 24f), "COCKTAIL RECIPES / VOL. 01", smallStyle);
                if (Button(new Rect(874f, 20f, 130f, 32f), "CLOSE  [ESC]")) { Close(); return; }
                for (int index = 0; index < 3; index++)
                    if (Button(new Rect(384f + index * 208f, 66f, 200f, 34f), shortTitles[index], selected == index))
                        SwitchTo(index);
                Fill(new Rect(352f, 112f, 1f, 487f), new Color(0.78f, 0.78f, 0.7f, 0.65f));
                DrawIllustration(new Rect(36f, 112f, 280f, 403f));
                Fill(new Rect(64f, 534f, 224f, 32f), new Color(accents[selected].r, accents[selected].g, accents[selected].b, 0.10f));
                GUI.Label(new Rect(64f, 534f, 224f, 32f), methods[selected], centeredStyle);
                GUI.Label(new Rect(36f, 577f, 280f, 24f), glasses[selected], centeredStyle);
                GUI.Label(new Rect(384f, 119f, 620f, 53f), titles[selected], titleStyle);
                GUI.Label(new Rect(384f, 180f, 620f, 25f), descriptions[selected], smallStyle);

                GUILayout.BeginArea(new Rect(384f, 223f, 620f, 362f));
                scroll = GUILayout.BeginScrollView(scroll, false, true);
                GUILayout.Label("INGREDIENTS", sectionStyle);
                GUILayout.Space(9f);
                for (int row = 0; row < ingredientNames[selected].Length; row++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(ingredientNames[selected][row], bodyStyle, GUILayout.MinHeight(25f));
                    GUILayout.Label(amounts[selected][row], amountStyle, GUILayout.Width(120f));
                    GUILayout.EndHorizontal();
                    Fill(GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true)), new Color(0.78f, 0.78f, 0.7f, 0.5f));
                    GUILayout.Space(6f);
                }
                GUILayout.Space(20f);
                GUILayout.Label("METHOD", sectionStyle);
                GUILayout.Space(12f);
                for (int step = 0; step < steps[selected].Length; step++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label((step + 1).ToString("00"), numberStyle, GUILayout.Width(35f));
                    GUILayout.Label(steps[selected][step], bodyStyle);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(12f);
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();

                Fill(new Rect(36f, 614f, 968f, 1f), new Color(0.78f, 0.78f, 0.7f));
                if (Button(new Rect(36f, 630f, 280f, 32f), "Start to make", true))
                {
                    owner.BeginPractice(selected);
                    GUIUtility.ExitGUI();
                }
                if (Button(new Rect(384f, 630f, 155f, 32f), "<  PREVIOUS")) SwitchTo(selected - 1);
                GUI.Label(new Rect(630f, 630f, 130f, 32f), "0" + (selected + 1) + " / 03", centeredStyle);
                if (Button(new Rect(849f, 630f, 155f, 32f), "NEXT  >")) SwitchTo(selected + 1);
            }
            finally { GUI.matrix = previousMatrix; }
        }

        public void Dispose()
        {
            Close();
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Material material in paper) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (Material material in artMaterials) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (Font owned in ownedFonts) if (owned != null) UnityEngine.Object.Destroy(owned);
            if (illustration != null) UnityEngine.Object.Destroy(illustration);
        }

        // Embedded artwork keeps the illustrated cards in the existing five-script Mac sync.
        private const string RecipeIllustrationBase64 = "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QBERAEUqEAREQBERAEREARFKAhERAEuiIAiIgCIiAIiIAiIgBREQBERARZSoUoAiXRAEREAREQBERAEREAREQBERAFKhEBKhFKAhERAEREAREQBERAEREAREsgCIiAIiIAiIgCKVCAIiIAiIgCIiAFQpUICUREAUKUQBERAERLIAiIgCXREARQpQBERAEREAREKAIgRAEREAREQBERAEREAREQBEKIAiIgCIiAIiIAiIgCIiAIl0QBERAQiIgJREQBERAEREAREQBEQIAiIgCIiAIiIApUIgJUKUUghFKhQAiIgCIiAIilAQiIEAREQEqERAEREARSoQBEUoCEREAsiIgCIiAIiIAiIgCIiAIiIAiIgCIiAIiIAiIgCIiAIiIAiFEAREQBFKhAERSgIREQBERAEREAsiIgCIiAIiIAiIgCJsiAIhRAEREBCkKFKAJdFCAlERAEREAREQBERAEREAREQBERAERSgCKEUgIiKAEREARFKAhERAEREAREQBERAERCgCIiAIiICVCIgCIiAIiIAiIgCJZEAREQBSoRASoUqFICKVCgBCpUIAiIgBREQBERAEREAREQBERAApUIgCIiAIiIAiIgCIUQBERAEREAS6IgCIiAIl0QBERAQpUKUBCKbIgCKFKAIiIAiIgCIiAIiIAiJZAEREAREQBCpUIBdERAESyIAiJZAERLIAiKEBKIiAIiIAiIgCIiAIidUAREQBERAEREAREQBERAEREAREQBEUoCFKhSgChSoQBERAEREAREQBERAEKIgBQIiAIiIBdERAESyIAiIgCIiAIiIAiIgCInwQBERAEREAREQH/9k=";
    }

}
