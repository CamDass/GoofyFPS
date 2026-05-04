using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

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
    public float lastShotTime;
    public bool isReloading;
    public float reloadStartTime;
    public float force;

    // constructeur
    public Weapon(string nom, int degats, int portee, float cadence, int munitionsMax, int tempsRecharge, Model modele3D, Sound son, float power)
    {
        name = nom;
        damage = degats;
        range = portee;
        fireRate = cadence;
        maxammo = munitionsMax;
        ammo = munitionsMax;
        reloadtime = tempsRecharge;
        modelname = modele3D;
        soundname = son;
        lastShotTime = 0.0f;
        isReloading = false;
        reloadStartTime = 0.0f;
        force = power;
    }



    public void Shoot()
    {
        
    }
    

    public void Reload()
    {
        if ((Raylib.IsKeyPressed(KeyboardKey.R) || ammo <= 0) && !isReloading)
        {
            isReloading = true;
            reloadStartTime = (float)Raylib.GetTime();
        }
        if (isReloading && (float)Raylib.GetTime() - reloadStartTime >= reloadtime)
        {
            ammo = maxammo;
            isReloading = false;
        }
    }

    public void Switch()
    {
        
    }
}


