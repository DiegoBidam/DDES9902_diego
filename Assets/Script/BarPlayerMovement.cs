using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class BarPlayerMovement : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float mouseSensitivity = 0.12f;

    private CharacterController controller;
    private UniformInteraction uniform;
    private BeerGlassPour beerInteraction;
    private Transform view;
    private float pitch;
    private float verticalSpeed;
    private GameObject boundaryRoot;
    private bool boundariesReady;
    private readonly Bounds[] stageBounds = new Bounds[2];
    private readonly Bounds[] floorBounds = new Bounds[3];

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        uniform = GetComponent<UniformInteraction>();
        beerInteraction = GetComponent<BeerGlassPour>();
        // 시작 안내로 이동이 비활성화되어도 양쪽 스테이지의 벽은 미리 만듭니다.
        CreateActivityWalls();
    }

    void Start()
    {
        Camera camera = GetComponentInChildren<Camera>();
        if (camera == null)
        {
            Debug.LogError("Player 아래에 Main Camera가 필요합니다.");
            enabled = false;
            return;
        }

        view = camera.transform;
    }

    void Update()
    {
        if (!boundariesReady)
            return;

        Vector2 movement = Vector2.zero;
        Vector2 look = Vector2.zero;
        bool clicked = false;
        bool escape = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            movement.x = (keyboard.dKey.isPressed ? 1 : 0)
                       - (keyboard.aKey.isPressed ? 1 : 0);
            movement.y = (keyboard.wKey.isPressed ? 1 : 0)
                       - (keyboard.sKey.isPressed ? 1 : 0);
            escape = keyboard.escapeKey.wasPressedThisFrame;
        }

        if (mouse != null)
        {
            look = mouse.delta.ReadValue() * mouseSensitivity;
            clicked = mouse.leftButton.wasPressedThisFrame;
        }
#else
        movement = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        look = new Vector2(
            Input.GetAxis("Mouse X"),
            Input.GetAxis("Mouse Y")
        ) * mouseSensitivity * 15f;

        clicked = Input.GetMouseButtonDown(0);
        escape = Input.GetKeyDown(KeyCode.Escape);
#endif

        // 안내 중에는 이동/시점/클릭 입력을 버리되 기존 중력 처리는 유지합니다.
        if ((uniform != null && uniform.IsGuidePlaying)
            || (beerInteraction != null && beerInteraction.IsRecipeOpen))
        {
            movement = Vector2.zero;
            look = Vector2.zero;
            clicked = false;
            escape = false;
        }

        if (escape)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (clicked && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            look = Vector2.zero;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            movement = Vector2.zero;
        }

        movement = Vector2.ClampMagnitude(movement, 1f);

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        verticalSpeed += Physics.gravity.y * Time.deltaTime;

        Vector3 velocity =
            (transform.right * movement.x +
             transform.forward * movement.y) * moveSpeed;

        // 단계 플래그 대신 현재 월드 위치로 구역을 선택하므로 출근 텔레포트도 그대로 허용합니다.
        Bounds area = NearestStage(transform.position);
        Vector3 target = LimitToFloor(transform.position + velocity * Time.deltaTime, area);
        float footOffset = (controller.center.y - controller.height * 0.5f) * transform.lossyScale.y;
        float minimumY = FloorHeight(target) - footOffset;
        float nextY = transform.position.y + verticalSpeed * Time.deltaTime;
        target.y = Mathf.Max(nextY, minimumY);
        if (nextY < minimumY)
            verticalSpeed = 0f;

        controller.Move(target - transform.position);

        // 충돌에 의한 밀림이나 충돌 누락에도 가장자리 밖/바닥 아래를 허용하지 않습니다.
        Vector3 corrected = LimitToFloor(transform.position, area);
        corrected.y = Mathf.Max(corrected.y, FloorHeight(corrected) - footOffset);
        if ((corrected - transform.position).sqrMagnitude > 0.00000001f)
        {
            controller.enabled = false;
            transform.position = corrected;
            controller.enabled = true;
            verticalSpeed = 0f;
        }
    }

    void CreateActivityWalls()
    {
        BoxCollider entranceFloor = null;
        BoxCollider uniformFloor = null;
        BoxCollider beerFloor = null;

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (BoxCollider floor in root.GetComponentsInChildren<BoxCollider>())
            {
                if (!floor.gameObject.activeInHierarchy || floor.isTrigger)
                    continue;

                switch (floor.name)
                {
                    case "OutsideFloor": entranceFloor = floor; break;
                    case "Floor": uniformFloor = floor; break;
                    case "BeerStage_Floor": beerFloor = floor; break;
                }
            }
        }

        if (entranceFloor == null || uniformFloor == null || beerFloor == null)
        {
            Debug.LogError("BarPlayerMovement: 투명 벽 생성에 OutsideFloor, Floor, BeerStage_Floor가 필요합니다.");
            enabled = false;
            return;
        }

        // 입장 구간과 준비실 사이에는 벽을 만들지 않고 외곽만 둘러쌉니다.
        // 물리 엔진 동기화 전에도 올바른 월드 경계를 얻습니다.
        floorBounds[0] = WorldBounds(entranceFloor);
        floorBounds[1] = WorldBounds(uniformFloor);
        floorBounds[2] = WorldBounds(beerFloor);
        stageBounds[0] = floorBounds[0];
        stageBounds[0].Encapsulate(floorBounds[1]);
        stageBounds[1] = floorBounds[2];

        boundaryRoot = new GameObject("ActivityBoundaries_Runtime");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boundaryRoot, gameObject.scene);
        CreatePerimeterWalls("Stage1", stageBounds[0]);
        CreatePerimeterWalls("Stage2", stageBounds[1]);
        Physics.SyncTransforms();
        boundariesReady = true;
    }

    static Bounds WorldBounds(BoxCollider floor)
    {
        Bounds result = new Bounds(floor.transform.TransformPoint(floor.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    result.Encapsulate(floor.transform.TransformPoint(floor.center
                        + Vector3.Scale(floor.size * 0.5f, new Vector3(x, y, z))));
        return result;
    }

    Bounds NearestStage(Vector3 position)
    {
        // 높이는 제외합니다. 낮은 위치에서도 현재 방을 유지하며 1단계로 되돌리지 않습니다.
        Vector3 flat = new Vector3(position.x, 0f, position.z);
        Bounds first = stageBounds[0];
        Bounds second = stageBounds[1];
        first.center = new Vector3(first.center.x, 0f, first.center.z);
        second.center = new Vector3(second.center.x, 0f, second.center.z);
        return first.SqrDistance(flat) <= second.SqrDistance(flat) ? stageBounds[0] : stageBounds[1];
    }

    Vector3 LimitToFloor(Vector3 position, Bounds area)
    {
        float margin = controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.z)) + controller.skinWidth + 0.05f;
        position.x = Mathf.Clamp(position.x, area.min.x + margin, area.max.x - margin);
        position.z = Mathf.Clamp(position.z, area.min.z + margin, area.max.z - margin);
        return position;
    }

    float FloorHeight(Vector3 position)
    {
        float height = float.NegativeInfinity;
        foreach (Bounds floor in floorBounds)
        {
            if (position.x >= floor.min.x && position.x <= floor.max.x
                && position.z >= floor.min.z && position.z <= floor.max.z)
                height = Mathf.Max(height, floor.max.y);
        }
        return height;
    }

    void CreatePerimeterWalls(string stage, Bounds floor)
    {
        const float thickness = 0.5f;
        const float inset = 0.05f;
        float height = Mathf.Max(6f, controller.height * 3f);
        float centerY = floor.min.y - 1f + height * 0.5f;

        // 벽의 안쪽 면을 바닥 가장자리보다 조금 안에 두어 발이 밖으로 나가지 않게 합니다.
        CreateWall(stage + "_Left",
            new Vector3(floor.min.x - thickness * 0.5f + inset, centerY, floor.center.z),
            new Vector3(thickness, height, floor.size.z + thickness * 2f));
        CreateWall(stage + "_Right",
            new Vector3(floor.max.x + thickness * 0.5f - inset, centerY, floor.center.z),
            new Vector3(thickness, height, floor.size.z + thickness * 2f));
        CreateWall(stage + "_Front",
            new Vector3(floor.center.x, centerY, floor.min.z - thickness * 0.5f + inset),
            new Vector3(floor.size.x + thickness * 2f, height, thickness));
        CreateWall(stage + "_Back",
            new Vector3(floor.center.x, centerY, floor.max.z + thickness * 0.5f - inset),
            new Vector3(floor.size.x + thickness * 2f, height, thickness));
    }

    void CreateWall(string wallName, Vector3 position, Vector3 size)
    {
        // Renderer 없이 충돌체만 생성합니다. 플레이어와 함께 이동하거나 비활성화되지 않습니다.
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(boundaryRoot.transform, false);
        wall.transform.position = position;
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = false;
    }

    void OnDestroy()
    {
        if (boundaryRoot != null)
            Destroy(boundaryRoot);
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}