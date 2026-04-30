using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;



using Raylib_cs; // Indispensable pour que le type "Model" soit reconnu

public class Weapon
{
    // parametres
    public string name;
    public int damage;
    public int range;
    public float fireRate;
    public int maxammo;
    public int ammo;
    public int reloadtime;
    public Model modelname;
    public Sound soundname;

    // constructeur
    public Weapon(string nom, int degats, int portee, float cadence, int munitionsMax, int tempsRecharge, Model modele3D, Sound son)
    {
        name = nom;
        damage = degats;
        range = portee;
        fireRate = cadence;
        maxammo = munitionsMax;
        reloadtime = tempsRecharge;
        modelname = modele3D;
        soundname = son;
    }



    public void Shoot()
    {
        
    }
    

    public void Reload()
    {
        if(Raylib.IsKeyPressed(KeyboardKey.R) || ammo <= 0)
        {
            Raylib.WaitTime(reloadtime);
            ammo = maxammo;
        }
    }

    public void Switch()
    {
        
    }
}


