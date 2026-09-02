using System;
using System.Numerics;
using Raylib_cs;

// ========================================================
// TEST VISUEL AUTOMATIQUE :  dotnet run -- --reglestest
// ========================================================
// Rend le panneau des mutateurs dans une RenderTexture et exporte trois PNG :
//   reglestest-hote.png    : ce que voit l'HÔTE (boutons [-] / [+], préréglages)
//   reglestest-client.png  : ce que voit un joueur qui a REJOINT (lecture seule)
//   reglestest-preset.png  : après le préréglage CHAOS, avec un (i) déplié
// Même principe que --skintest : on regarde l'image, pas le code.
// ========================================================
partial class Program
{
    public static void LancerReglesTest()
    {
        Raylib.InitWindow(1920, 1080, "GoofyFPS - ReglesTest");
        Raylib.SetTargetFPS(60);

        RenderTexture2D rt = Raylib.LoadRenderTexture(1920, 1080);

        // --- VUE 1 : l'hôte, règles d'usine ---
        MatchRules.Reinitialiser();
        isOnline = true; isServer = true;
        RendrePanneau(rt, true, "reglestest-hote.png");

        // --- VUE 2 : le client (lecture seule), avec des règles bien modifiées ---
        MatchRules.Poser(MatchRules.Get("gravite"), 0.35f);
        MatchRules.Poser(MatchRules.Get("vie"), 25f);
        MatchRules.Poser(MatchRules.Get("munitions"), 0f);
        MatchRules.Poser(MatchRules.Get("vue"), 60f);
        MatchRules.BasculerArme(0); // on retire le sniper
        MatchRules.BasculerArme(2); // on retire le bazooka
        isServer = false;
        RendrePanneau(rt, false, "reglestest-client.png");

        // --- VUE 3 : préréglage CHAOS + une explication dépliée ---
        isServer = true;
        foreach (MatchRules.Preset p in MatchRules.Presets)
            if (p.Nom == "CHAOS") p.Poser();
        infoEpinglee = "explosions";
        RendrePanneau(rt, true, "reglestest-preset.png");

        // --- VUE 4 : le même panneau en ONGLET des Paramètres (vérifie qu'il tient
        //     sous les onglets et au-dessus du bouton RETOUR) ---
        infoEpinglee = "";
        isServer = false;
        RendreOngletParametres(rt, "reglestest-onglet.png");

        Raylib.UnloadRenderTexture(rt);
        Raylib.CloseWindow();

        VerifierAllerRetourReseau();

        Console.WriteLine("[REGLESTEST] reglestest-hote.png / reglestest-client.png / "
                        + "reglestest-preset.png / reglestest-onglet.png");
    }

    // Reproduit le cadre de l'écran Paramètres autour du panneau (titre + 3 onglets +
    // bouton RETOUR) pour vérifier qu'il n'y a aucun chevauchement.
    static void RendreOngletParametres(RenderTexture2D rt, string fichier)
    {
        int sw = 1920, sh = 1080;
        for (int frame = 0; frame < 2; frame++)
        {
            MenuNav.Begin(777100);
            Raylib.BeginTextureMode(rt);
            Raylib.ClearBackground(new Color(30, 30, 30, 255));

            Raylib.DrawText("PARAMÈTRES", sw / 2 - Raylib.MeasureText("PARAMÈTRES", 50) / 2, 40, 50, Color.White);
            string[] onglets = { "GÉNÉRAL", "RACCOURCIS", "RÈGLES" };
            for (int i = 0; i < 3; i++)
            {
                Rectangle b = new Rectangle(sw / 2 - 330 + i * 225, 120, 210, 40);
                Raylib.DrawRectangleRec(b, i == 2 ? Color.Red : Color.DarkGray);
                Raylib.DrawRectangleLinesEx(b, 2, Color.Black);
                int w = Raylib.MeasureText(onglets[i], 18);
                Raylib.DrawText(onglets[i], (int)(b.X + (210 - w) / 2), (int)b.Y + 11, 18, Color.White);
            }

            DessinerPanneauRegles(editable: false, dessinerFond: false, avecTitre: false,
                                  avecBoutonFermer: false, yDepart: 175);

            Rectangle btnRetour = new Rectangle(sw / 2 - 100, sh - 100, 200, 50);
            Raylib.DrawRectangleRec(btnRetour, Color.DarkGray);
            Raylib.DrawText("RETOUR", (int)btnRetour.X + 55, (int)btnRetour.Y + 15, 20, Color.White);

            Raylib.EndTextureMode();
            MenuNav.End();
        }

        Image img = Raylib.LoadImageFromTexture(rt.Texture);
        Raylib.ImageFlipVertical(ref img);
        Raylib.ExportImage(img, fichier);
        Raylib.UnloadImage(img);
    }

    // ========================================================
    // CONTRÔLE DE L'ALLER-RETOUR RÉSEAU
    // ========================================================
    // Le paquet transporte les valeurs DANS L'ORDRE de MatchRules.Liste : si quelqu'un
    // insère une règle au milieu de la liste un jour, ce test le voit tout de suite.
    static void VerifierAllerRetourReseau()
    {
        // 0. Chaque valeur d'usine doit tomber PILE sur un cran : sinon Poser() la
        //    recale et le réglage "par défaut" dérive dès la première synchro.
        MatchRules.Reinitialiser();
        int horsGrille = 0;
        foreach (MatchRules.Regle r in MatchRules.Liste)
        {
            MatchRules.Poser(r, r.Defaut);
            if (!r.EstDefaut)
            {
                Console.WriteLine($"[REGLESTEST] DÉFAUT HORS GRILLE sur '{r.Cle}' : "
                                + $"{r.Defaut} recalé en {r.Valeur} (min {r.Min}, pas {r.Pas})");
                horsGrille++;
            }
        }
        Console.WriteLine($"[REGLESTEST] Défauts alignés sur le pas : {MatchRules.Liste.Count - horsGrille}"
                        + $"/{MatchRules.Liste.Count}");

        foreach (MatchRules.Preset p in MatchRules.Presets)
            if (p.Nom == "HARDCORE") p.Poser();

        float[] avant = new float[MatchRules.Liste.Count];
        for (int i = 0; i < avant.Length; i++) avant[i] = MatchRules.Liste[i].Valeur;

        // Sérialisation (côté hôte) puis désérialisation (côté client)
        var ecrit = new LiteNetLib.Utils.NetDataWriter();
        Packets.MatchRulesPacket envoi = new Packets.MatchRulesPacket { Valeurs = avant };
        envoi.Serialize(ecrit);

        MatchRules.Reinitialiser(); // le "client" repart de zéro
        Packets.MatchRulesPacket recu = new Packets.MatchRulesPacket();
        recu.Deserialize(new LiteNetLib.Utils.NetDataReader(ecrit.Data, 0, ecrit.Length));

        isServer = false; // sinon OnMatchRulesReceived ignore le paquet (autorité hôte)
        OnMatchRulesReceived(recu, null);

        int fautes = 0;
        for (int i = 0; i < avant.Length; i++)
            if (MathF.Abs(MatchRules.Liste[i].Valeur - avant[i]) > 0.0001f)
            {
                Console.WriteLine($"[REGLESTEST] ÉCART sur '{MatchRules.Liste[i].Cle}' : "
                                + $"envoyé {avant[i]}, reçu {MatchRules.Liste[i].Valeur}");
                fautes++;
            }

        // L'hôte, lui, DOIT ignorer un paquet de règles (un client ne règle jamais rien)
        MatchRules.Reinitialiser();
        isServer = true;
        OnMatchRulesReceived(recu, null);
        bool hoteIgnore = !MatchRules.ReglesModifiees();
        isServer = false;

        Console.WriteLine($"[REGLESTEST] Aller-retour réseau : {avant.Length} règles, {fautes} écart(s). "
                        + $"Paquet client refusé par l'hôte : {(hoteIgnore ? "OUI" : "NON !!")}");

        // Le tirage "chaos" ne doit JAMAIS toucher les armes ni se désactiver lui-même,
        // et il doit sauter les règles solo quand on est en ligne.
        MatchRules.Reinitialiser();
        for (int i = 0; i < 8; i++)
            Console.WriteLine("[REGLESTEST] chaos (en ligne) -> " + MatchRules.TirerModificateurAleatoire(true));
        bool armesIntactes = MatchRules.Get("armes").EstDefaut;
        bool chaosIntact = MatchRules.Get("chaos").EstDefaut;
        bool zombiesIntacts = MatchRules.Get("zombies").EstDefaut && MatchRules.Get("zvie").EstDefaut
                              && MatchRules.Get("zvitesse").EstDefaut && MatchRules.Get("zboom").EstDefaut;
        Console.WriteLine($"[REGLESTEST] chaos : armes intactes {armesIntactes}, "
                        + $"interrupteur chaos intact {chaosIntact}, règles solo épargnées {zombiesIntacts}");

        MatchRules.Reinitialiser();
    }

    static void RendrePanneau(RenderTexture2D rt, bool editable, string fichier)
    {
        // Deux frames : MenuNav a besoin d'une frame pour compter ses items avant de
        // pouvoir placer le focus (countLast est rempli par End()).
        for (int frame = 0; frame < 2; frame++)
        {
            MenuNav.Begin(777000 + (editable ? 1 : 0));
            Raylib.BeginTextureMode(rt);
            Raylib.ClearBackground(new Color(28, 30, 38, 255));
            DessinerPanneauRegles(editable);
            Raylib.EndTextureMode();
            MenuNav.End();
        }

        Image img = Raylib.LoadImageFromTexture(rt.Texture);
        Raylib.ImageFlipVertical(ref img); // les RenderTextures OpenGL sont à l'envers
        Raylib.ExportImage(img, fichier);
        Raylib.UnloadImage(img);
    }
}
