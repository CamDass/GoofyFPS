using System;

public class Player
{
    // --- Statistiques ---
    public int MaxHealth;
    public int Health;
    public bool IsAlive;

    // --- Constructeur ---
    public Player(int maxHealth = 100)
    {
        MaxHealth = maxHealth;
        Health = MaxHealth;
        IsAlive = true;
    }

    // --- Actions ---
    public void TakeDamage(int amount)
    {
        if (!IsAlive) return; // On ne tape pas un cadavre !

        Health -= amount;
        
        // Effet visuel ou sonore possible ici plus tard (ex: écran rouge)

        if (Health <= 0)
        {
            Health = 0;
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;

        Health += amount;
        if (Health > MaxHealth) Health = MaxHealth; // On ne dépasse pas le max
    }

    public void Respawn()
    {
        Health = MaxHealth;
        IsAlive = true;
    }

    private void Die()
    {
        IsAlive = false;
        Console.WriteLine("[INFO] Le joueur est mort !");
        // Plus tard, on appellera un écran de Game Over ici
    }
}