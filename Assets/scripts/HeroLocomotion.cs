using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class HeroLocomotion : MonoBehaviour
{
    private const float MinMoveSpeedSquared = 0.04f;
    private const float BlendTime = 0.12f;

    [SerializeField] private Animation heroAnimation;

    private Rigidbody body;
    private bool running;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void LateUpdate()
    {
        if (heroAnimation == null || Time.timeScale == 0f)
            return;

        Vector3 velocity = body.velocity;
        bool moving = velocity.x * velocity.x + velocity.z * velocity.z > MinMoveSpeedSquared;
        if (running == moving)
            return;

        running = moving;
        heroAnimation.CrossFade(running ? "Run" : "Idle", BlendTime);
    }
}
