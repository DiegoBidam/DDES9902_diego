using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-100)]
public class UniformInteraction : MonoBehaviour
{
    public Camera playerCamera;
    public GameObject shirt;
    public GameObject apron;
    public GameObject clockGuide;
    public Transform beerStageSpawn;

    public float interactDistance = 2.5f;
    // 기존 Inspector 직렬화 호환용. 진행 대기는 실제 음성 종료를 사용합니다.
    public float shiftMessageDuration = 3f;

    public AudioClip afterShirtVoice;
    public AudioClip afterApronVoice;
    public AudioClip shiftStartVoice;

    public AudioSource entranceVoiceSource;

    public bool ShirtWorn { get; private set; }
    public bool ApronWorn { get; private set; }
    public bool IsFullyDressed => ShirtWorn && ApronWorn;
    public bool ShiftStarted => shiftStarted;
    public bool ShiftCompleted { get; private set; }
    public bool IsGuidePlaying => showingShiftMessage || guidePlaybackFrame == Time.frameCount
        || (clothingVoiceSource != null && clothingVoiceSource.isActiveAndEnabled && clothingVoiceSource.isPlaying)
        || (entranceVoiceSource != null && entranceVoiceSource.isActiveAndEnabled && entranceVoiceSource.isPlaying)
        || (entranceGuide != null && entranceGuide.IsPlaying);

    private EntranceGuide entranceGuide;
    private int guidePlaybackFrame = -1;

    public void RegisterEntranceGuide(EntranceGuide guide)
    {
        entranceGuide = guide;
    }

    private bool shiftStarted;
    private bool showingShiftMessage;
    private AudioSource clothingVoiceSource;
    private BarPlayerMovement movement;
    private string prompt = "";
    private string subtitle = "";
    private GUIStyle textStyle;
    private GameObject uniformSignRoot;
    private Transform shirtSign;
    private Transform apronSign;
    private TextMesh shirtSignHint;
    private TextMesh apronSignHint;
    private Material signBackground;
    private Material shirtSignAccent;
    private Material apronSignAccent;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        movement = GetComponent<BarPlayerMovement>();

        if (playerCamera == null || shirt == null || apron == null)
        {
            Debug.LogError("UniformInteraction: Player Camera, Shirt, Apron을 연결하세요.");
            enabled = false;
            return;
        }

        clothingVoiceSource = gameObject.AddComponent<AudioSource>();
        clothingVoiceSource.playOnAwake = false;
        clothingVoiceSource.loop = false;
        clothingVoiceSource.spatialBlend = 0f;

        HideClockGuide();
        CreateUniformSigns();
    }

    void Update()
    {
        prompt = "";

        if (IsGuidePlaying)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Transform target = hit.collider.transform;

        if (!ShirtWorn && target.IsChildOf(shirt.transform))
        {
            prompt = "E - Put on shirt";

            if (PressedInteract())
            {
                if (entranceGuide != null)
                    entranceGuide.TryPlayInstruction();

                if (IsGuidePlaying)
                {
                    prompt = "";
                    return;
                }

                ShirtWorn = true;
                shirt.SetActive(false);
                prompt = "";
                PlayGuide(afterShirtVoice, "Great. Now put on your apron.");
            }

            return;
        }

        if (!ApronWorn && target.IsChildOf(apron.transform))
        {
            if (!ShirtWorn)
            {
                prompt = "Put on your shirt first.";
                return;
            }

            prompt = "E - Put on apron";

            if (PressedInteract())
            {
                ApronWorn = true;
                apron.SetActive(false);
                prompt = "";

                PlayGuide(
                    afterApronVoice,
                    "You're all set. Head behind the counter to your station."
                );

                ShowClockGuide();
            }

            return;
        }

        if (IsFullyDressed && !shiftStarted && clockGuide != null && target.IsChildOf(clockGuide.transform))
        {
            prompt = "E - Start shift";

            if (PressedInteract())
            {
                StartCoroutine(StartShiftRoutine());
            }
        }
    }

    private IEnumerator StartShiftRoutine()
    {
        shiftStarted = true;
        showingShiftMessage = true;
        prompt = "";
        subtitle = "Shift started. Welcome to the beer station.";

        HideClockGuide();

        if (movement != null)
            movement.enabled = false;

        PlayGuide(shiftStartVoice, subtitle);

        if (beerStageSpawn != null)
        {
            CharacterController controller = GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            transform.position = beerStageSpawn.position;
            // 현재 스폰은 출입구 쪽을 향하므로 바 안쪽으로 돌아서 시작합니다.
            transform.rotation = beerStageSpawn.rotation * Quaternion.Euler(0f, 180f, 0f);
            if (movement != null)
                movement.ResetViewPitch();

            if (controller != null)
                controller.enabled = true;
        }
        else
        {
            Debug.LogWarning("BeerStageSpawn이 연결되지 않았습니다.");
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Play 직후의 프레임을 넘긴 뒤 실제 재생 종료를 기다립니다.
        // 클립/소스가 없거나 재생할 수 없다면 다음 프레임에 진행합니다.
        yield return null;
        while (clothingVoiceSource != null && clothingVoiceSource.isActiveAndEnabled
            && clothingVoiceSource.isPlaying)
        {
            yield return null;
        }

        showingShiftMessage = false;
        ShiftCompleted = true;

        if (movement != null)
            movement.enabled = true;
    }

    private void ShowClockGuide()
    {
        if (clockGuide == null)
            return;

        clockGuide.SetActive(true);

        Renderer clockRenderer = clockGuide.GetComponent<Renderer>();
        Collider clockCollider = clockGuide.GetComponent<Collider>();

        if (clockRenderer != null)
            clockRenderer.enabled = true;

        if (clockCollider != null)
            clockCollider.enabled = true;
    }

    private void HideClockGuide()
    {
        if (clockGuide == null)
            return;

        clockGuide.SetActive(true);

        Renderer clockRenderer = clockGuide.GetComponent<Renderer>();
        Collider clockCollider = clockGuide.GetComponent<Collider>();

        if (clockRenderer != null)
            clockRenderer.enabled = false;

        if (clockCollider != null)
            clockCollider.enabled = false;
    }

    void PlayGuide(AudioClip clip, string text)
    {
        subtitle = text;

        if (clip == null || clothingVoiceSource == null || !clothingVoiceSource.isActiveAndEnabled)
        {
            Debug.LogWarning("안내 음성 파일이 연결되지 않았습니다.");
            return;
        }

        guidePlaybackFrame = Time.frameCount;
        clothingVoiceSource.clip = clip;
        clothingVoiceSource.Play();
    }

    void CreateUniformSigns()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;
        signBackground = new Material(shader) { color = new Color(0.035f, 0.085f, 0.1f) };
        shirtSignAccent = new Material(shader) { color = new Color(0.3f, 0.85f, 0.78f) };
        apronSignAccent = new Material(shader) { color = new Color(1f, 0.72f, 0.3f) };
        uniformSignRoot = new GameObject("UniformGuideSigns_Runtime");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(uniformSignRoot, gameObject.scene);
        shirtSign = CreateUniformSign(shirt, "T-SHIRT", shirtSignAccent, out shirtSignHint);
        apronSign = CreateUniformSign(apron, "APRON", apronSignAccent, out apronSignHint);
    }

    Transform CreateUniformSign(GameObject item, string title, Material accent, out TextMesh hint)
    {
        Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        }
        GameObject sign = new GameObject(title + "_GuideSign");
        sign.transform.SetParent(uniformSignRoot.transform, false);
        sign.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z);
        AddSignPanel(sign.transform, "Nameplate", Vector3.zero, new Vector3(0.92f, 0.32f, 0.018f), signBackground);
        AddSignPanel(sign.transform, "Accent", new Vector3(-0.445f, 0f, -0.012f), new Vector3(0.018f, 0.32f, 0.008f), accent);
        AddSignText(sign.transform, title, new Vector3(0.01f, 0.065f, -0.014f), 0.82f, 0.115f, true);
        hint = AddSignText(sign.transform, "", new Vector3(0.01f, -0.075f, -0.014f), 0.82f, 0.065f, false);
        return sign.transform;
    }

    void AddSignPanel(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = position;
        panel.transform.localScale = size;
        panel.GetComponent<Renderer>().sharedMaterial = material;
        // 표지는 착용 Raycast나 플레이어 이동을 가리지 않습니다.
        Collider collider = panel.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
    }

    TextMesh AddSignText(Transform parent, string content, Vector3 position, float width, float height, bool bold)
    {
        GameObject writing = new GameObject("SignText");
        writing.transform.SetParent(parent, false);
        writing.transform.localPosition = position;
        TextMesh text = writing.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.characterSize = bold ? 0.08f : 0.05f;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.97f, 0.98f, 0.92f);
        writing.GetComponent<Renderer>().sharedMaterial = text.font.material;
        FitSignText(text, content, width, height);
        return text;
    }

    void FitSignText(TextMesh text, string content, float width, float height)
    {
        if (text.text == content && content.Length > 0) return;
        text.font.RequestCharactersInTexture(content, text.fontSize, text.fontStyle);
        text.text = content;
        text.transform.localScale = Vector3.one;
        Bounds bounds = text.GetComponent<Renderer>().localBounds;
        if (bounds.size.x > 0f && bounds.size.y > 0f)
            text.transform.localScale = Vector3.one * Mathf.Min(width / bounds.size.x, height / bounds.size.y);
    }

    void LateUpdate()
    {
        UpdateUniformSign(shirtSign, shirt, ShirtWorn, shirtSignHint,
            IsGuidePlaying ? "Listen to the guide..." : "Aim at shirt + press E");
        UpdateUniformSign(apronSign, apron, ApronWorn, apronSignHint,
            !ShirtWorn ? "Wear the T-shirt first" : IsGuidePlaying ? "Listen to the guide..." : "Aim at apron + press E");
    }

    void UpdateUniformSign(Transform sign, GameObject item, bool worn, TextMesh hint, string instruction)
    {
        if (sign == null || playerCamera == null) return;
        bool visible = !worn && !shiftStarted && item != null && item.activeInHierarchy;
        sign.gameObject.SetActive(visible);
        if (!visible) return;
        Vector3 awayFromCamera = sign.position - playerCamera.transform.position;
        if (awayFromCamera.sqrMagnitude > 0.0001f)
            sign.rotation = Quaternion.LookRotation(awayFromCamera, Vector3.up);
        FitSignText(hint, instruction, 0.82f, 0.065f);
    }

    void OnDestroy()
    {
        if (uniformSignRoot != null) Destroy(uniformSignRoot);
        if (signBackground != null) Destroy(signBackground);
        if (shirtSignAccent != null) Destroy(shirtSignAccent);
        if (apronSignAccent != null) Destroy(apronSignAccent);
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
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 20;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.wordWrap = true;
            textStyle.normal.textColor = Color.white;
        }

        string status =
            "Shirt: " + (ShirtWorn ? "ON" : "--") +
            "    Apron: " + (ApronWorn ? "ON" : "--");

        GUI.Box(new Rect(15, 15, 320, 45), "");
        GUI.Label(new Rect(15, 15, 320, 45), status, textStyle);

        if (showingShiftMessage)
        {
            float width = Mathf.Min(640f, Screen.width - 30f);
            Rect area = new Rect(
                (Screen.width - width) / 2f,
                Screen.height / 2f - 70f,
                width,
                140f
            );

            GUI.Box(area, "");
            GUI.Label(
                area,
                "Shift started.\nWelcome to the beer station.",
                textStyle
            );

            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            GUI.Label(
                new Rect(0, Screen.height / 2f + 40, Screen.width, 45),
                "Click the game view to start",
                textStyle
            );
        }
        else
        {
            GUI.Label(
                new Rect(Screen.width / 2f - 15, Screen.height / 2f - 15, 30, 30),
                "+",
                textStyle
            );

            if (!string.IsNullOrEmpty(prompt))
            {
                GUI.Label(
                    new Rect(0, Screen.height / 2f + 40, Screen.width, 45),
                    prompt,
                    textStyle
                );
            }
        }

        if (clothingVoiceSource != null && clothingVoiceSource.isPlaying)
        {
            float width = Mathf.Min(640f, Screen.width - 30f);
            Rect area = new Rect(
                (Screen.width - width) / 2f,
                Screen.height - 150f,
                width,
                70f
            );

            GUI.Box(area, "");
            GUI.Label(area, subtitle, textStyle);
        }
        else if (IsFullyDressed && !shiftStarted)
        {
            GUI.Label(
                new Rect(0, Screen.height - 80, Screen.width, 60),
                "Uniform complete! Head to the bar station.",
                textStyle
            );
        }
    }
}