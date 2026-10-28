using System;
using UnityEngine;

// Porte-monnaie du joueur. Une seule instance dans la scène.
public class Wallet : MonoBehaviour
{
    int money;

    public int Money => money;
    public event Action<int> OnChanged;

    public void Add(int amount)
    {
        money += amount;
        OnChanged?.Invoke(money);
    }
}
