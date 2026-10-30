using UnityEngine;

// Camionnette du client (11c) : arrive au quai quand une commande est acceptée, attend avec une bulle d'humeur
// (content -> neutre -> en colère selon la patience), repart avec des confettis si livrée, en colère si le délai est écoulé.
public class DeliveryVan : MonoBehaviour
{
    enum State { Away, Arriving, Parked, Leaving }

    [SerializeField] OrderBoard board;
    [SerializeField] Transform awayPoint;     // hors de l'atelier
    [SerializeField] Transform parkPoint;     // au quai
    [SerializeField] SpriteRenderer moodBubble;
    [SerializeField] Sprite[] moodSprites;    // content, neutre, en colère
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField, Min(0.1f)] float travelTime = 2f;

    State state = State.Away;
    float t;
    bool leaveHappy;

    void Awake()
    {
        transform.position = awayPoint.position;
        if (moodBubble != null) moodBubble.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        board.OnAccepted += OnAccepted;
        board.OnCompleted += OnCompleted;
        board.OnFailed += OnFailed;
    }

    void OnDisable()
    {
        board.OnAccepted -= OnAccepted;
        board.OnCompleted -= OnCompleted;
        board.OnFailed -= OnFailed;
    }

    void OnAccepted(OrderBoard.Offer o)
    {
        state = State.Arriving;
        t = 0f;
        if (cameraFollow != null) cameraFollow.Peek(parkPoint, 1.4f);
    }

    void OnCompleted(OrderBoard.Offer o)
    {
        leaveHappy = true;
        if (feedback != null) feedback.Confetti(transform.position + Vector3.up * 1.5f);
        AudioManager.Play(SfxId.Unlock);
        Leave();
    }

    void OnFailed(OrderBoard.Offer o)
    {
        leaveHappy = false;
        Leave();
    }

    void Leave()
    {
        state = State.Leaving;
        t = 0f;
    }

    void Update()
    {
        switch (state)
        {
            case State.Arriving:
                t += Time.deltaTime / travelTime;
                transform.position = Vector3.Lerp(awayPoint.position, parkPoint.position, Smooth(t));
                if (t >= 1f) state = State.Parked;
                break;
            case State.Leaving:
                t += Time.deltaTime / travelTime;
                transform.position = Vector3.Lerp(parkPoint.position, awayPoint.position, Smooth(t));
                if (t >= 1f) state = State.Away;
                break;
        }

        // bulle d'humeur : visible quand la camionnette est garée ou qu'elle repart
        bool show = state == State.Parked || state == State.Arriving || state == State.Leaving;
        if (moodBubble != null)
        {
            if (moodBubble.gameObject.activeSelf != show) moodBubble.gameObject.SetActive(show);
            if (show && moodSprites != null && moodSprites.Length >= 3)
            {
                int mood = state == State.Leaving ? (leaveHappy ? 0 : 2) : board.Mood;
                moodBubble.sprite = moodSprites[mood];
            }
        }
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
