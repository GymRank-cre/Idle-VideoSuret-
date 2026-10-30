using UnityEngine;
using UnityEngine.InputSystem;

// Déplace le joueur. Clavier (WASD / flèches) dans l'éditeur,
// joystick tactile sur mobile via SetMoveInput().
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] UpgradeState upgrades; // facultatif : amélioration de vitesse
    [SerializeField] float turnSpeed = 540f;
    [SerializeField, Min(0f)] float inputSmoothTime = 0.08f; // lisse le tremblement du pouce (9b : sans lissage, ±8° à 6 Hz = ±8° de rotation)
    [SerializeField, Min(0f)] float turnDeadAngle = 5f;       // pas de pivot pour un écart plus petit tant que le personnage va tout droit
    [SerializeField] Transform cameraTransform; // vide = Camera.main

    CharacterController controller;
    Vector2 externalInput;
    Vector2 smoothedInput;
    Vector2 inputVelocity;
    bool turning;

    public bool IsMoving { get; private set; }
    public float MoveAmount { get; private set; }  // intensité de l'entrée, de 0 à 1 (lecture seule)
    public float PlanarSpeed { get; private set; } // vitesse au sol en m/s (lecture seule)

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    // Appelé par le joystick tactile (valeurs de -1 à 1).
    public void SetMoveInput(Vector2 input)
    {
        externalInput = input;
    }

    Vector2 ReadInput()
    {
        Vector2 input = externalInput;

        var kb = Keyboard.current;
        if (kb != null)
        {
            Vector2 k = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1f;
            if (k != Vector2.zero) input = k;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    void Update()
    {
        Vector2 input = Vector2.SmoothDamp(smoothedInput, ReadInput(), ref inputVelocity, inputSmoothTime);
        if (input.sqrMagnitude < 1e-4f) { input = Vector2.zero; inputVelocity = Vector2.zero; }
        smoothedInput = input;

        // Mouvement relatif à la caméra, à plat sur le sol.
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }

        Vector3 planar = (forward * input.y + right * input.x) * moveSpeed * (upgrades != null ? upgrades.PlayerSpeed : 1f) * Bonuses.PlayerSpeed;
        Vector3 velocity = planar;
        velocity.y = -2f; // plaque le personnage au sol

        controller.Move(velocity * Time.deltaTime);

        IsMoving = planar.sqrMagnitude > 0.01f;
        MoveAmount = input.magnitude;
        PlanarSpeed = planar.magnitude;
        if (IsMoving)
        {
            Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
            float gap = Quaternion.Angle(transform.rotation, target);
            if (gap > turnDeadAngle) turning = true;       // vrai virage : on pivote jusqu'à la bonne direction
            else if (gap < 1f) turning = false;
            if (turning)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, turnSpeed * Time.deltaTime);
        }
        else
        {
            turning = false;
        }
    }
}
