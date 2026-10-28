using System;
using System.Collections.Generic;
using UnityEngine;

// Un client : marche en ligne droite le long de points de passage (pas de NavMesh), porte sa commande
// et sa bulle, et anime son modèle (Idle / Walk) d'après sa vitesse de déplacement.
public class Customer : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

    [SerializeField] Animator animator;
    [SerializeField] OrderBubble bubble;
    [SerializeField] float turnSpeed = 420f;     // degrés par seconde
    [SerializeField] float walkClipSpeed = 1.4f; // m/s couverts par l'animation de marche
    [SerializeField] float walkParam = 0.3f;     // valeur du paramètre Speed qui déclenche l'état Walk

    readonly List<Vector3> path = new List<Vector3>();
    Action<Customer> onPathDone;
    Vector3 finalFacing = Vector3.forward;
    float speed;
    int delivered;

    public OrderBubble Bubble => bubble;
    public ProductType OrderType { get; private set; }
    public int Quantity { get; private set; }
    public int Remaining => Quantity - delivered;
    public bool IsComplete => Quantity > 0 && delivered >= Quantity;
    public bool IsWalking => path.Count > 0;

    public void StartOrder(ProductType type, int quantity)
    {
        OrderType = type;
        Quantity = quantity;
        delivered = 0;
        bubble.Hide();
    }

    // Marche vers chaque point dans l'ordre ; `facing` est la direction prise à l'arrivée.
    public void WalkPath(IList<Vector3> points, float walkSpeed, Vector3 facing, Action<Customer> done)
    {
        path.Clear();
        path.AddRange(points);
        speed = walkSpeed;
        finalFacing = facing;
        onPathDone = done;
    }

    // Le client avance dans la file : on change seulement sa destination finale (les points déjà prévus restent).
    public void ReplaceFinalTarget(Vector3 target, float walkSpeed, Vector3 facing, Action<Customer> done)
    {
        speed = walkSpeed;
        finalFacing = facing;
        onPathDone = done;
        if (path.Count > 0) path[path.Count - 1] = target;
        else path.Add(target);
    }

    public void ShowOrder()
    {
        bubble.Show(OrderType, Remaining);
    }

    public void Receive()
    {
        delivered++;
        bubble.Refresh(OrderType, Mathf.Max(Remaining, 0));
        if (IsComplete) bubble.Complete();
    }

    public void HideOrder()
    {
        bubble.Hide();
    }

    void Update()
    {
        bool moving = false;
        if (path.Count > 0)
        {
            Vector3 pos = transform.position;
            Vector3 target = path[0];
            target.y = pos.y;
            Vector3 delta = target - pos;
            float step = speed * Time.deltaTime;

            if (delta.magnitude <= step)
            {
                transform.position = target;
                path.RemoveAt(0);
                if (path.Count == 0)
                {
                    Action<Customer> done = onPathDone;
                    onPathDone = null;
                    done?.Invoke(this);
                }
            }
            else
            {
                Vector3 dir = delta.normalized;
                transform.position = pos + dir * step;
                Turn(dir);
                moving = true;
            }
        }
        else
        {
            Turn(finalFacing);
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, moving ? walkParam : 0f);
            animator.SetFloat(AnimSpeedHash, moving ? Mathf.Clamp(speed / walkClipSpeed, 0.5f, 1.6f) : 1f);
        }
    }

    void Turn(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }
}
