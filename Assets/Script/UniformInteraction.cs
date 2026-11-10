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
            transform.rotation = beerStageSpawn.rotation;

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