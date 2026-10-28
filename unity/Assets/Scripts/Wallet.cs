using System;
using UnityEngine;

// Porte-monnaie du joueur. Une seule instance dans la scène.
// Seul endroit où l'argent change : Add() et TrySpend().
public class Wallet : MonoBehaviour
{
    [SerializeField] int startMoney; // pour les tests, 0 en jeu normal

    int money;

    public int Money => money;
    public event Action<int> OnChanged;

    void Awake()
    {
        money = startMoney;
    }

    public void Add(int amount)
    {
        money += amount;
        OnChanged?.Invoke(money);
    }

    // Retire `amount` si le joueur a assez ; sinon ne touche à rien et renvoie false.
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > money) return false;

        money -= amount;
        OnChanged?.Invoke(money);
        return true;
    }
}
