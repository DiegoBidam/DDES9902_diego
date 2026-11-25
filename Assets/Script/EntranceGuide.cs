using UnityEngine;

// 입구 안내 → 착용 처리 → 이동 입력 순서로 같은 프레임의 중복 입력을 막습니다.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(AudioSource))]
public class EntranceGuide : MonoBehaviour
{
    public Transform player;
    public AudioClip shirtInstruction;

    private BoxCollider entranceZone;
    private AudioSource voiceSource;
    private bool hasPlayed;
    private int playbackFrame = -1;
    private UniformInteraction uniform;

    public bool IsPlaying => playbackFrame == Time.frameCount
        || (voiceSource != null && voiceSource.isActiveAndEnabled && voiceSource.isPlaying);

    void Awake()
    {
        entranceZone = GetComponent<BoxCollider>();
        entranceZone.isTrigger = true;

        voiceSource = GetComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.Stop();

        if (player != null)
        {
            uniform = player.GetComponent<UniformInteraction>();
            if (uniform != null)
                uniform.RegisterEntranceGuide(this);
        }
    }

    void Start()
    {
        if (player == null)
        {
            Debug.LogError(
                "EntranceGuide: Player를 연결하세요."
            );
            enabled = false;
        }
    }

    void Update()
    {
        if (hasPlayed)
            return;

        // 플레이어의 발보다 조금 위를 기준으로 입장 여부 확인
        Vector3 checkPoint = player.position + Vector3.up * 0.9f;

        if (entranceZone.bounds.Contains(checkPoint))
        {
            TryPlayInstruction();
        }
    }

    // 셔츠를 먼저 조준해도 입구 안내를 선행하고, 늦은 입구 재생은 막습니다.
    public void TryPlayInstruction()
    {
        if (hasPlayed || !isActiveAndEnabled)
            return;

        if (uniform != null)
        {
            if (uniform.ShirtWorn)
            {
                hasPlayed = true;
                return;
            }

            if (!uniform.isActiveAndEnabled || uniform.IsGuidePlaying)
                return;
        }

        hasPlayed = true;
        if (shirtInstruction == null || voiceSource == null || !voiceSource.isActiveAndEnabled)
            return;

        playbackFrame = Time.frameCount;
        voiceSource.clip = shirtInstruction;
        voiceSource.Play();
    }

    private WorldPanel entranceBoard;
    void LateUpdate()
    {
        if (!IsPlaying || player == null) { if (entranceBoard != null) entranceBoard.Hide(); return; }
        Camera camera = player.GetComponentInChildren<Camera>();
        if (camera == null) return;
        if (entranceBoard == null) entranceBoard = new WorldPanel(gameObject, "Entrance_InstructionPlaque", 620f, 170f, 0.0018f);
        Vector3 anchor = entranceZone.bounds.center + new Vector3(-1.25f, 0f, 0.5f); anchor.y = 2.10f;
        entranceBoard.Pose(anchor, camera); entranceBoard.Begin();
        entranceBoard.Text(new Rect(20f, 16f, 580f, 38f), "WELCOME / GET READY", 29, WorldPanel.Accent, TextAnchor.MiddleCenter, true);
        entranceBoard.Text(new Rect(20f, 70f, 580f, 84f), "Let's get you ready. Put on your shirt first.", 28, null, TextAnchor.MiddleCenter);
        entranceBoard.End();
    }
    void OnDisable() { if (entranceBoard != null) entranceBoard.Hide(); }
    void OnDestroy() { if (entranceBoard != null) entranceBoard.Dispose(); }
}
