using UnityEngine;

// Réglages d'un employé automatique. Rien n'est en dur dans les scripts.
[CreateAssetMenu(fileName = "EmployeeConfig", menuName = "Idle/Employee Config")]
public class EmployeeConfig : ScriptableObject
{
    [SerializeField, Min(0.5f)] float moveSpeed = 3f;     // m/s
    [SerializeField, Min(1)] int capacity = 6;            // caisses portées dans le dos
    [SerializeField, Min(0f)] float restDuration = 0.4f;  // pause entre deux étapes (pile pleine, poste vidé)
    [SerializeField, Min(1)] int hirePrice = 150;         // prix de la dalle d'embauche

    public float MoveSpeed => moveSpeed;
    public int Capacity => capacity;
    public float RestDuration => restDuration;
    public int HirePrice => hirePrice;
}
