using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BarIntro : MonoBehaviour
{
    public AudioClip welcomeVoice;

    private AudioSource voiceSource;
    private BarPlayerMovement movement;
    private UniformInteraction uniform;

    private bool started;
    private bool finished;
    private GUIStyle messageStyle;

    void Awake()
    {
        movement = GetComponent<BarPlayerMovement>();
        uniform = GetComponent<UniformInteraction>();
        voiceSource = GetComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.Stop();

        if (movement == null || uniform == null)
        {
            Debug.LogError("BarIntro는 player에 붙여주세요.");
            enabled = false;
            return;
        }

        movement.enabled = false;
        uniform.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    IEnumerator PlayIntro()
    {
        started = true;

        voiceSource.clip = welcomeVoice;
        voiceSource.Play();

        // 재생이 시작된 다음 종료될 때까지 기다립니다.
        yield return null;

        while (voiceSource.isPlaying)
            yield return null;

        finished = true;

        movement.enabled = true;
        uniform.enabled = true;

        // 음성이 끝난 뒤 Game 화면 클릭으로 시점을 잠급니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (finished)
            return;

        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.fontSize = 22;
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.wordWrap = true;
            messageStyle.normal.textColor = Color.white;
        }

        float width = Mathf.Min(560f, Screen.width - 30f);
        float left = (Screen.width - width) / 2f;
        float top = Screen.height / 2f - 80f;

        GUI.Box(new Rect(left, top, width, 160f), "");

        if (!started)
        {
            GUI.Label(
                new Rect(left + 10, top + 15, width - 20, 50),
                "Your first shift",
                messageStyle
            );

            if (welcomeVoice == null)
            {
                GUI.Label(
                    new Rect(left + 10, top + 70, width - 20, 70),
                    "Assign the Welcome Voice clip in the Inspector.",
                    messageStyle
                );
                return;
            }

            if (GUI.Button(
                new Rect(Screen.width / 2f - 100, top + 85, 200, 50),
                "Click to start"))
            {
                StartCoroutine(PlayIntro());
            }
        }
        else
        {
            GUI.Label(
                new Rect(left + 15, top + 15, width - 30, 130),
                "Welcome to your first shift!\nHead inside the bar.",
                messageStyle
            );
        }
    }
}