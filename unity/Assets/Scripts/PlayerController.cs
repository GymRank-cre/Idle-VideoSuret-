using UnityEngine;
using UnityEngine.InputSystem;

// Déplace le joueur. Clavier (WASD / flèches) dans l'éditeur,
// joystick tactile sur mobile via SetMoveInput().
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float turnSpeed = 720f;
    [SerializeField] Transform cameraTransform; // vide = Camera.main

    CharacterController controller;
    Vector2 externalInput;

    public bool IsMoving { get; private set; }

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
        Vector2 input = ReadInput();

        // Mouvement relatif à la caméra, à plat sur le sol.
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }

        Vector3 planar = (forward * input.y + right * input.x) * moveSpeed;
        Vector3 velocity = planar;
        velocity.y = -2f; // plaque le personnage au sol

        controller.Move(velocity * Time.deltaTime);

        IsMoving = planar.sqrMagnitude > 0.01f;
        if (IsMoving)
        {
            Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
