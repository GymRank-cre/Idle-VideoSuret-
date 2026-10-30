using System;
using UnityEngine;

// Points Pro (11c) : seconde devise, gagnée en livrant des commandes. Pas encore dépensable (11d).
public class ProPoints : MonoBehaviour
{
    int points;

    public int Points => points;
    public event Action<int> OnChanged;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        points += amount;
        OnChanged?.Invoke(points);
    }

    // Dépense `amount` Points Pro s'il y en a assez ; sinon ne touche à rien.
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > points) return false;
        points -= amount;
        OnChanged?.Invoke(points);
        return true;
    }

    // Restauration depuis la sauvegarde.
    public void Set(int value)
    {
        points = Mathf.Max(0, value);
        OnChanged?.Invoke(points);
    }
}
