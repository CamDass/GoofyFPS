using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

public class Player
{
    // --- Statistiques ---
    static Sound gameover = Raylib.LoadSound("assets\\sounds\\gameover.mp3");
    static Sound paw = Raylib.LoadSound("assets\\sounds\\paw.mp3");
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
        
        // Effet visuel : overlay rouge
        Program.damageOverlayOpacity = 0.4f; // 40% d'opacité
        
        // Effet sonore : son de dégât
        Raylib.PlaySound(Program.hitSound);

        if (Health <= 0)
        {
            Health = 0;
            Die();
            Raylib.PlaySound(paw);
            Raylib.PlaySound(gameover);
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
        Health = 0;
        Console.WriteLine("[INFO] Le joueur est mort !");
    }
}