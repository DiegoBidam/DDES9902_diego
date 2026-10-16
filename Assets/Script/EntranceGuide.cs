using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(AudioSource))]
public class EntranceGuide : MonoBehaviour
{
    public Transform player;
    public AudioClip shirtInstruction;

    private BoxCollider entranceZone;
    private AudioSource voiceSource;
    private bool hasPlayed;
    private GUIStyle subtitleStyle;

    void Awake()
    {
        entranceZone = GetComponent<BoxCollider>();
        entranceZone.isTrigger = true;

        voiceSource = GetComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.Stop();
    }

    void Start()
    {
        if (player == null || shirtInstruction == null)
        {
            Debug.LogError(
                "EntranceGuide: Player와 Shirt Instruction을 연결하세요."
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
            hasPlayed = true;
            voiceSource.clip = shirtInstruction;
            voiceSource.Play();
        }
    }

    void OnGUI()
    {
        if (voiceSource == null || !voiceSource.isPlaying)
            return;

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.fontSize = 22;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.wordWrap = true;
            subtitleStyle.normal.textColor = Color.white;
        }

        float width = Mathf.Min(640f, Screen.width - 30f);
        Rect area = new Rect(
            (Screen.width - width) / 2f,
            Screen.height - 150f,
            width,
            65f
        );

        GUI.Box(area, "");
        GUI.Label(
            area,
            "Let's get you ready. Put on your shirt first.",
            subtitleStyle
        );
    }
}