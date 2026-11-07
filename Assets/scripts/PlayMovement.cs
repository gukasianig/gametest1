using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class PlayMovement : MonoBehaviour
{

    public float speed = 5f;
    public float SpeedMultiplier { get; set; } = 1f;
    //public Transform cameraTransform;
    public float rotationSpeed = 10f;
    public Transform cameraTransform;

    Rigidbody rb;
    private Vector2 movementInput;
    private Vector3 aimDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    [Header("Dash")]
    public float dashDistance = 7f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 4f;
    public float dashInvulnerability = 0.4f;
    private float dashTimeLeft, nextDash;
    private bool dashRequested;
    private Vector3 dashDirection;
    private Text dashLabel;
    private Canvas dashCanvas;
    public bool Dashing => dashTimeLeft > 0f;
    public float DashCooldownRemaining => Mathf.Max(0f, nextDash - Time.time);

    void Update()
    {
        movementInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        bool playing = GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;
        if (playing && Time.timeScale > 0f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift)))
            dashRequested = true;
        UpdateDashLabel(playing);
    }

    // Public so checks can trigger a dash without a keyboard.
    public void RequestDash() { dashRequested = true; }

    private void UpdateDashLabel(bool playing)
    {
        if (dashLabel == null)
        {
            if (!playing) return;
            dashCanvas = GameUi.Canvas("Dash HUD", 79);
            RectTransform box = GameUi.Box(dashCanvas.transform, "Dash", new Vector2(0.8f, 0.03f), new Vector2(0.97f, 0.08f), GameUi.Background);
            dashLabel = GameUi.Label(box, "", Vector2.zero, Vector2.one, 20);
        }
        dashCanvas.enabled = playing;
        float left = DashCooldownRemaining;
        dashLabel.text = left <= 0f ? "Рывок: готов (Пробел)" : "Рывок: " + left.ToString("0.0") + " с";
        dashLabel.color = left <= 0f ? GameUi.Accent : new Color(0.6f, 0.66f, 0.72f);
    }

    void OnDestroy()
    {
        if (dashCanvas != null) Destroy(dashCanvas.gameObject);
    }

    public void SetAimDirection(Vector3 direction)
    {
        direction.y = 0f;
        aimDirection = direction;
    }

    void FixedUpdate()
    {
        if (cameraTransform == null) return;
        float x = movementInput.x;
        float z = movementInput.y;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = Vector3.ClampMagnitude(forward * z + right * x, 1f);

        if (dashRequested)
        {
            dashRequested = false;
            if (Time.time >= nextDash && dashTimeLeft <= 0f)
            {
                dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
                dashDirection.y = 0f;
                dashTimeLeft = Mathf.Max(0.05f, dashDuration);
                nextDash = Time.time + Mathf.Max(1f, dashCooldown - CombatStats.DashCooldownReduction);
                PlayerHealth health = GetComponent<PlayerHealth>();
                if (health != null) health.GrantInvulnerability(dashInvulnerability);
            }
        }
        Vector3 velocity = move * speed * SpeedMultiplier;
        if (dashTimeLeft > 0f)
        {
            dashTimeLeft -= Time.fixedDeltaTime;
            velocity = dashDirection * ((dashDistance + CombatStats.DashDistanceBonus) / Mathf.Max(0.05f, dashDuration));
        }
        if (ArenaLayout.Instance != null)
        {
            Vector3 desired = rb.position + velocity * Time.fixedDeltaTime;
            Vector3 allowed = ArenaLayout.Instance.ResolveMovement(rb.position, desired, 0.5f);
            velocity = (allowed - rb.position) / Time.fixedDeltaTime;
        }
        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
        //transform.Translate(move * speed * Time.deltaTime, Space.World);

            Vector3 facing = aimDirection.sqrMagnitude > 0.001f ? aimDirection : move;
            if (facing.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facing);
                rb.MoveRotation(Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSpeed * Time.fixedDeltaTime)
                ));
            }



    }


}
