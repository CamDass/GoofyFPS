using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;

// ========================================================
// LES MUTATEURS DE MATCH ("RÈGLES")
// ========================================================
// UN SEUL endroit décrit chaque réglage : son nom, sa plage, son pas, sa valeur par
// défaut, la façon de l'afficher et le texte du (i). L'UI (MenuRegles) et le réseau
// (Packets.MatchRulesPacket) se contentent de parcourir cette liste : ajouter un
// mutateur = ajouter UNE ligne dans Definir(), rien d'autre côté menu ni côté réseau.
//
// AUTORITÉ : c'est l'HÔTE qui règle. Le client reçoit la liste et l'affiche en lecture
// seule (MenuRegles passe editable=false). Un paquet de règles venant d'un client est
// jeté (voir Program.OnMatchRulesReceived).
//
// ORDRE DES RÈGLES = ORDRE RÉSEAU : on n'insère jamais une règle au milieu de la liste,
// on l'ajoute à la fin (sinon les valeurs se décalent entre deux versions du jeu).
// ========================================================
public static class MatchRules
{
    public const byte VersionReseau = 1;

    public enum Genre { Multiplicateur, Entier, Interrupteur, Masque }

    public class Regle
    {
        public string Cle;            // identifiant stable (debug / presets)
        public string Nom;            // libellé affiché
        public string Categorie;      // regroupement dans le menu
        public string Info;           // texte du bouton (i)
        public float Min, Max, Pas, Defaut;
        public Genre Type;
        public bool SoloSeulement;    // grisé en ligne (les zombies ne sont pas synchronisés)
        public Func<float, string> Format;
        public float Valeur;

        public string Texte => Format != null ? Format(Valeur) : Valeur.ToString("0.##");
        public bool EstDefaut => MathF.Abs(Valeur - Defaut) < 0.0001f;
    }

    public static readonly List<Regle> Liste = new List<Regle>();
    static readonly Dictionary<string, Regle> parCle = new Dictionary<string, Regle>();

    // Les armes, dans l'ORDRE EXACT de Program.weapons (le masque est un bit par arme).
    public static readonly string[] NomsArmes = { "Sniper", "Karambit", "Bazooka", "Shotgun", "Pistolet", "Revolver", "Épée" };
    public const int MasqueToutesArmes = (1 << 7) - 1;

    // ========================================================
    // LES VALEURS PRÊTES À L'EMPLOI (recalculées à chaque changement)
    // Le gameplay lit CES champs-là, jamais la liste : zéro recherche par frame.
    // ========================================================
    public static float GraviteMul = 1f;
    public static int   SautsAir = 1;
    public static float VitesseMul = 1f;
    public static float GlisseFacteur = 1f;   // multiplie l'accélération au sol (1 = normal, ~0.05 = patinoire)
    public static float ReculMul = 1f;
    public static bool  ChuteMortelle = false;
    public static float DegatsMul = 1f;
    public static float MunitionsMul = 1f;
    public static bool  MunitionsInfinies = false;
    public static float CadenceMul = 1f;
    public static int   VieMax = 100;
    public static float RegenParSec = 0f;
    public static float ExplosionMul = 1f;
    public static int   MasqueArmes = MasqueToutesArmes;
    public static int   NbBarils = 10;
    public static int   MurDelaiSec = 10;
    public static float DistanceVue = 150f;
    public static int   ZombiesMax = 40;
    public static float ZombieVitesseMul = 1f;
    public static int   ZombieVie = 100;
    public static float ZombieExplosifPct = 0f;
    public static bool  ChaosActif = false;

    // ========================================================
    // LA DÉFINITION DES RÈGLES
    // ========================================================
    static MatchRules() { Definir(); Recalculer(); }

    static void Ajouter(Regle r)
    {
        r.Valeur = r.Defaut;
        Liste.Add(r);
        parCle[r.Cle] = r;
    }

    static string Mult(float v) => "x" + v.ToString("0.00");

    static void Definir()
    {
        // ---------------- PHYSIQUE ----------------
        Ajouter(new Regle {
            Cle = "gravite", Nom = "Gravité", Categorie = "PHYSIQUE", Type = Genre.Multiplicateur,
            Min = 0.25f, Max = 3f, Pas = 0.05f, Defaut = 1f, Format = Mult,
            Info = "Multiplie l'attraction vers le bas (x1 = -10 m/s2).\n" +
                   "En dessous de x0.5 on flotte : les duels se jouent en l'air et les sauts\n" +
                   "portent trois fois plus loin. Au-dessus de x2 on retombe comme une enclume :\n" +
                   "le wall-run et le double saut deviennent indispensables."
        });
        Ajouter(new Regle {
            Cle = "sauts", Nom = "Sauts en l'air", Categorie = "PHYSIQUE", Type = Genre.Entier,
            Min = 0f, Max = 4f, Pas = 1f, Defaut = 1f,
            Format = v => v <= 0 ? "0 (aucun)" : (v == 1 ? "1 (double saut)" : (int)v + " sauts"),
            Info = "Nombre de sauts SUPPLÉMENTAIRES une fois quitté le sol.\n" +
                   "0 = plus de double saut (mode dur). 4 = parkour flottant.\n" +
                   "Indépendant de la gravité : les deux se combinent."
        });
        Ajouter(new Regle {
            Cle = "vitesse", Nom = "Vitesse de course", Categorie = "PHYSIQUE", Type = Genre.Multiplicateur,
            Min = 0.5f, Max = 2.5f, Pas = 0.1f, Defaut = 1f, Format = Mult,
            Info = "Multiplie la vitesse maximale à pied (marche ET sprint).\n" +
                   "N'affecte ni le dash ni la glissade : à x2 on court presque aussi vite\n" +
                   "qu'une glissade, viser devient beaucoup plus dur."
        });
        Ajouter(new Regle {
            Cle = "glisse", Nom = "Glisse du sol", Categorie = "PHYSIQUE", Type = Genre.Entier,
            Min = 0f, Max = 100f, Pas = 10f, Defaut = 0f,
            Format = v => v <= 0 ? "0 % (normal)" : (int)v + " %" + (v >= 100 ? " (patinoire)" : ""),
            Info = "Réduit l'adhérence au sol : on accélère et on freine beaucoup plus lentement.\n" +
                   "À 100 % la map devient une patinoire, plus personne ne s'arrête net\n" +
                   "et tout le monde rate ses tirs. Aucun effet en l'air."
        });
        Ajouter(new Regle {
            Cle = "recul", Nom = "Recul des armes", Categorie = "PHYSIQUE", Type = Genre.Multiplicateur,
            Min = 0f, Max = 5f, Pas = 0.5f, Defaut = 1f, Format = Mult,
            Info = "Multiplie la poussée que le tir applique au tireur.\n" +
                   "À x4, viser le sol avec le bazooka ou le shotgun devient un rocket-jump.\n" +
                   "Sans effet sur les armes qui n'ont pas de recul (sniper, karambit, pistolet)."
        });
        Ajouter(new Regle {
            Cle = "chute", Nom = "Dégâts de chute", Categorie = "PHYSIQUE", Type = Genre.Interrupteur,
            Min = 0f, Max = 1f, Pas = 1f, Defaut = 0f,
            Format = v => v > 0 ? "ACTIVÉS" : "OFF",
            Info = "Atterrir trop vite fait mal : au-delà de 25 m/s de vitesse verticale,\n" +
                   "chaque m/s supplémentaire coûte des PV.\n" +
                   "Se marie très mal avec une grosse gravité - c'est le but."
        });

        // ---------------- COMBAT ----------------
        Ajouter(new Regle {
            Cle = "degats", Nom = "Dégâts", Categorie = "COMBAT", Type = Genre.Multiplicateur,
            Min = 0.25f, Max = 5f, Pas = 0.25f, Defaut = 1f, Format = Mult,
            Info = "Multiplie les dégâts de TOUTES les armes (joueurs, zombies, explosions).\n" +
                   "Combiné à une petite vie max, on passe en mode one-shot.\n" +
                   "L'hôte applique la même limite aux tirs reçus : impossible de tricher dessus."
        });
        Ajouter(new Regle {
            Cle = "munitions", Nom = "Munitions", Categorie = "COMBAT", Type = Genre.Multiplicateur,
            Min = 0f, Max = 5f, Pas = 0.25f, Defaut = 1f,
            Format = v => v <= 0 ? "INFINIES" : Mult(v),
            Info = "Multiplie la taille des chargeurs (minimum 1 balle).\n" +
                   "Poussé tout à gauche : munitions INFINIES, plus aucune recharge.\n" +
                   "À x0.25 le sniper tombe à 1 balle : chaque tir compte."
        });
        Ajouter(new Regle {
            Cle = "cadence", Nom = "Cadence de tir", Categorie = "COMBAT", Type = Genre.Multiplicateur,
            Min = 0.25f, Max = 4f, Pas = 0.25f, Defaut = 1f, Format = Mult,
            Info = "Multiplie la vitesse de tir (le délai entre deux balles est divisé d'autant).\n" +
                   "x4 + munitions infinies = arrosage permanent.\n" +
                   "L'anti-triche de l'hôte suit automatiquement la nouvelle cadence."
        });
        Ajouter(new Regle {
            Cle = "vie", Nom = "Vie maximale", Categorie = "COMBAT", Type = Genre.Entier,
            Min = 25f, Max = 250f, Pas = 25f, Defaut = 100f,
            Format = v => (int)v + " PV",
            Info = "Points de vie de chaque joueur au spawn.\n" +
                   "25 PV = le moindre tir tue. 250 PV = duels très longs, l'esquive prime.\n" +
                   "Les barils soignent toujours de 25 PV."
        });
        Ajouter(new Regle {
            Cle = "regen", Nom = "Régénération", Categorie = "COMBAT", Type = Genre.Entier,
            Min = 0f, Max = 20f, Pas = 2f, Defaut = 0f,
            Format = v => v <= 0 ? "OFF" : (int)v + " PV/s",
            Info = "Soin automatique après 5 secondes sans prendre de dégâts.\n" +
                   "Encourage à décrocher du combat plutôt qu'à chercher un baril.\n" +
                   "Le compteur repart à zéro à chaque balle reçue."
        });
        Ajouter(new Regle {
            Cle = "explosions", Nom = "Puissance explosions", Categorie = "COMBAT", Type = Genre.Multiplicateur,
            Min = 0f, Max = 4f, Pas = 0.25f, Defaut = 1f, Format = Mult,
            Info = "Multiplie le RAYON et les dégâts du bazooka et des barils (6 m de base).\n" +
                   "À x3 une seule explosion peut faire sauter la moitié d'une map en chaîne.\n" +
                   "À x0 les explosions deviennent purement décoratives."
        });
        Ajouter(new Regle {
            Cle = "armes", Nom = "Armes autorisées", Categorie = "COMBAT", Type = Genre.Masque,
            Min = 0f, Max = MasqueToutesArmes, Pas = 1f, Defaut = MasqueToutesArmes,
            Format = v => CompterArmes((int)v) + " / " + NomsArmes.Length,
            Info = "Coche les armes qui peuvent sortir des barils et servir d'arme de départ.\n" +
                   "N'en laisser qu'une = mode imposé (sniper only, couteau only...).\n" +
                   "Au moins une arme reste toujours cochée."
        });

        // ---------------- MONDE ----------------
        Ajouter(new Regle {
            Cle = "barils", Nom = "Barils sur la map", Categorie = "MONDE", Type = Genre.Entier,
            Min = 0f, Max = 30f, Pas = 1f, Defaut = 10f,
            Format = v => v <= 0 ? "AUCUN" : ((int)v).ToString(),
            Info = "Nombre de barils présents en même temps (ils réapparaissent en 30 s).\n" +
                   "Un baril = 25 PV de soin + une arme au hasard + une explosion.\n" +
                   "À 0 on garde l'arme de départ toute la partie ; à 30 la map est minée."
        });
        Ajouter(new Regle {
            Cle = "mur", Nom = "Recharge du mur", Categorie = "MONDE", Type = Genre.Entier,
            Min = 0f, Max = 30f, Pas = 1f, Defaut = 10f,
            Format = v => v <= 0 ? "INSTANTANÉ" : (int)v + " s",
            Info = "Délai avant de pouvoir reposer un mur de construction.\n" +
                   "À 0 c'est du spam de murs : on se bâtit une forteresse en courant.\n" +
                   "À 30 s le mur devient une ressource qu'on garde pour se sauver."
        });
        Ajouter(new Regle {
            // Pas de 25 à partir de 50 : la valeur d'usine (150 m, le brouillard
            // d'origine du jeu) tombe PILE sur un cran. Un défaut hors grille serait
            // recalé par Poser() et dériverait à la première synchro réseau.
            Cle = "vue", Nom = "Distance de vue", Categorie = "MONDE", Type = Genre.Entier,
            Min = 50f, Max = 600f, Pas = 25f, Defaut = 150f,
            Format = v => (int)v + " m" + (v >= 600 ? " (clair)" : (v <= 60 ? " (brouillard)" : "")),
            Info = "Distance à laquelle le brouillard avale complètement le décor.\n" +
                   "À 60 m on ne voit plus venir : le sniper perd tout, le couteau devient roi.\n" +
                   "À 600 m la map est parfaitement lisible d'un bout à l'autre."
        });

        // ---------------- ZOMBIES (solo) ----------------
        Ajouter(new Regle {
            Cle = "zombies", Nom = "Zombies max", Categorie = "ZOMBIES", Type = Genre.Entier,
            Min = 0f, Max = 60f, Pas = 5f, Defaut = 40f, SoloSeulement = true,
            Format = v => v <= 0 ? "AUCUN" : ((int)v).ToString(),
            Info = "Nombre de zombies vivants en même temps (un nouveau toutes les 3 s).\n" +
                   "0 désactive complètement la horde.\n" +
                   "SOLO uniquement : les zombies ne sont pas synchronisés entre les machines."
        });
        Ajouter(new Regle {
            Cle = "zvitesse", Nom = "Vitesse des zombies", Categorie = "ZOMBIES", Type = Genre.Multiplicateur,
            Min = 0.5f, Max = 2.5f, Pas = 0.1f, Defaut = 1f, SoloSeulement = true, Format = Mult,
            Info = "Multiplie la vitesse de poursuite ET d'errance des zombies.\n" +
                   "Un seul zombie à x2.5 fait plus peur que quarante à x1.\n" +
                   "SOLO uniquement."
        });
        Ajouter(new Regle {
            Cle = "zvie", Nom = "Vie des zombies", Categorie = "ZOMBIES", Type = Genre.Entier,
            Min = 25f, Max = 400f, Pas = 25f, Defaut = 100f, SoloSeulement = true,
            Format = v => (int)v + " PV",
            Info = "Points de vie de chaque zombie à l'apparition.\n" +
                   "25 PV : le pistolet suffit. 400 PV : il faut le sniper ou le bazooka.\n" +
                   "SOLO uniquement."
        });
        Ajouter(new Regle {
            Cle = "zboom", Nom = "Zombies explosifs", Categorie = "ZOMBIES", Type = Genre.Entier,
            Min = 0f, Max = 100f, Pas = 10f, Defaut = 0f, SoloSeulement = true,
            Format = v => v <= 0 ? "OFF" : (int)v + " %",
            Info = "Proportion de zombies qui EXPLOSENT en mourant (même souffle qu'un baril).\n" +
                   "Tuer au corps-à-corps devient un pari ; à 100 % la horde est un champ de mines.\n" +
                   "SOLO uniquement."
        });

        // ---------------- CHAOS ----------------
        Ajouter(new Regle {
            Cle = "chaos", Nom = "Modificateur aléatoire", Categorie = "CHAOS", Type = Genre.Interrupteur,
            Min = 0f, Max = 1f, Pas = 1f, Defaut = 0f,
            Format = v => v > 0 ? "ACTIVÉ / 60 s" : "OFF",
            Info = "Pendant le match, l'hôte tire un réglage au hasard toutes les 60 secondes,\n" +
                   "lui donne une valeur au hasard et l'annonce à l'écran.\n" +
                   "Un seul interrupteur pour un match différent à chaque fois."
        });
    }

    public static int CompterArmes(int masque)
    {
        int n = 0;
        for (int i = 0; i < NomsArmes.Length; i++) if ((masque & (1 << i)) != 0) n++;
        return n;
    }

    // ========================================================
    // ACCÈS / MODIFICATION
    // ========================================================
    public static Regle Get(string cle) => parCle.TryGetValue(cle, out Regle r) ? r : null;

    public static float Valeur(string cle) { Regle r = Get(cle); return r != null ? r.Valeur : 0f; }

    /// <summary>Pose une valeur (bornée + alignée sur le pas) et recalcule les raccourcis.</summary>
    public static void Poser(Regle r, float v)
    {
        if (r == null) return;
        v = Math.Clamp(v, r.Min, r.Max);
        if (r.Type != Genre.Masque && r.Pas > 0f)
            v = r.Min + MathF.Round((v - r.Min) / r.Pas) * r.Pas;
        r.Valeur = Math.Clamp(v, r.Min, r.Max);
        Recalculer();
    }

    public static void Increment(Regle r, int sens) => Poser(r, r.Valeur + sens * r.Pas);

    /// <summary>Coche/décoche une arme (on refuse de tout décocher).</summary>
    public static void BasculerArme(int index)
    {
        Regle r = Get("armes");
        if (r == null || index < 0 || index >= NomsArmes.Length) return;
        int masque = (int)r.Valeur ^ (1 << index);
        if (CompterArmes(masque) == 0) return; // il faut toujours au moins une arme
        Poser(r, masque);
    }

    public static bool ArmeAutorisee(int index)
    {
        if (index < 0 || index >= NomsArmes.Length) return true;
        return (MasqueArmes & (1 << index)) != 0;
    }

    public static void Reinitialiser()
    {
        foreach (Regle r in Liste) r.Valeur = r.Defaut;
        Recalculer();
    }

    /// <summary>Vrai si au moins un réglage n'est plus à sa valeur d'usine.</summary>
    public static bool ReglesModifiees()
    {
        foreach (Regle r in Liste) if (!r.EstDefaut) return true;
        return false;
    }

    /// <summary>Résumé court des réglages modifiés, pour le salon (ex. "Gravité x0.35, Vie 25 PV").</summary>
    public static string Resume(int maxEntrees = 3)
    {
        List<string> bouts = new List<string>();
        foreach (Regle r in Liste)
        {
            if (r.EstDefaut) continue;
            if (bouts.Count >= maxEntrees) { bouts.Add("..."); break; }
            bouts.Add(r.Nom + " " + r.Texte);
        }
        return bouts.Count == 0 ? "Règles standard" : string.Join("   -   ", bouts);
    }

    // ========================================================
    // LES RACCOURCIS (appelé après CHAQUE changement de valeur)
    // ========================================================
    public static void Recalculer()
    {
        GraviteMul        = Valeur("gravite");
        SautsAir          = (int)Valeur("sauts");
        VitesseMul        = Valeur("vitesse");
        GlisseFacteur     = 1f - (Valeur("glisse") / 100f) * 0.95f; // 0 % -> 1.0 ; 100 % -> 0.05
        ReculMul          = Valeur("recul");
        ChuteMortelle     = Valeur("chute") > 0f;
        DegatsMul         = Valeur("degats");
        MunitionsMul      = Valeur("munitions");
        MunitionsInfinies = MunitionsMul <= 0f;
        CadenceMul        = Valeur("cadence");
        VieMax            = (int)Valeur("vie");
        RegenParSec       = Valeur("regen");
        ExplosionMul      = Valeur("explosions");
        MasqueArmes       = (int)Valeur("armes");
        if (CompterArmes(MasqueArmes) == 0) MasqueArmes = MasqueToutesArmes;
        NbBarils          = (int)Valeur("barils");
        MurDelaiSec       = (int)Valeur("mur");
        DistanceVue       = Valeur("vue");
        ZombiesMax        = (int)Valeur("zombies");
        ZombieVitesseMul  = Valeur("zvitesse");
        ZombieVie         = (int)Valeur("zvie");
        ZombieExplosifPct = Valeur("zboom");
        ChaosActif        = Valeur("chaos") > 0f;
    }

    // ========================================================
    // APPLICATION AU MONDE
    // ========================================================
    // Les multiplicateurs (dégâts, cadence, vitesse...) sont lus en direct par le
    // gameplay. Ici on ne pose QUE ce qui doit être écrit une bonne fois : la gravité
    // du moteur physique, le nombre de sauts, la vie max, la horde et les barils.
    // À appeler au lancement d'un match (solo ET réseau) et à chaque changement reçu.
    static float derniereGraviteAppliquee = float.NaN; // pour ne journaliser qu'aux changements

    public static void Appliquer()
    {
        Recalculer();

        // 1. La gravité du moteur BEPU (elle vit dans les callbacks de l'intégrateur)
        if (Program.simulation != null
            && Program.simulation.PoseIntegrator is PoseIntegrator<PoseIntegratorCallbacks> integrateur)
        {
            float g = -10f * GraviteMul;
            // (toute comparaison avec NaN est fausse : on teste le 1er appel à part)
            if (float.IsNaN(derniereGraviteAppliquee) || MathF.Abs(g - derniereGraviteAppliquee) > 0.0001f)
            {
                derniereGraviteAppliquee = g;
                Console.WriteLine($"[MUTATEURS] Gravité du moteur physique : {g:0.00} m/s2 (x{GraviteMul:0.00})");
            }
            integrateur.Callbacks.Gravity = new Vector3(0f, g, 0f);
        }

        // 2. Le joueur local
        Program.NbJumpMax = SautsAir;
        Program.localPlayer.MaxHealth = VieMax;
        if (Program.localPlayer.Health > VieMax) Program.localPlayer.Health = VieMax;

        // 3. La horde (solo uniquement - InitEnnemis coupe tout de toute façon en ligne)
        Program.maxZombies = ZombiesMax;
        Program.zombiesActifs = ZombiesMax > 0;

        // 4. Les barils
        Program.initialBarrelCount = NbBarils;

        // 5. Les armes : chargeurs recalés sur la nouvelle taille, arme en main
        //    remplacée si elle vient d'être décochée.
        Program.RecalibrerArmes();
    }

    // ========================================================
    // LES PRÉRÉGLAGES (un clic = un match complet)
    // ========================================================
    public class Preset
    {
        public string Nom;
        public string Info;
        public Action Poser;
    }

    static void Set(string cle, float v) => Poser(Get(cle), v);

    public static readonly List<Preset> Presets = new List<Preset>
    {
        new Preset {
            Nom = "NORMAL",
            Info = "Remet tous les réglages à leur valeur d'usine.",
            Poser = () => Reinitialiser()
        },
        new Preset {
            Nom = "LUNE",
            Info = "Gravité x0.35, 3 sauts en l'air, recul x2.\nLes duels se jouent entièrement en l'air.",
            Poser = () => { Reinitialiser(); Set("gravite", 0.35f); Set("sauts", 3f); Set("recul", 2f); }
        },
        new Preset {
            Nom = "HARDCORE",
            Info = "25 PV, aucun saut en l'air, munitions x0.5, dégâts x2.\nUne balle bien placée suffit.",
            Poser = () => { Reinitialiser(); Set("vie", 25f); Set("sauts", 0f); Set("munitions", 0.5f); Set("degats", 2f); }
        },
        new Preset {
            Nom = "CHAOS",
            Info = "Barils partout, explosions x2, cadence x2, munitions infinies\net un modificateur tiré au hasard toutes les 60 s.",
            Poser = () => { Reinitialiser(); Set("barils", 30f); Set("explosions", 2f); Set("cadence", 2f); Set("munitions", 0f); Set("chaos", 1f); }
        },
        new Preset {
            Nom = "PATINOIRE",
            Info = "Glisse 100 %, vitesse x1.4, recul x3, dégâts de chute.\nPlus personne ne s'arrête.",
            Poser = () => { Reinitialiser(); Set("glisse", 100f); Set("vitesse", 1.4f); Set("recul", 3f); Set("chute", 1f); }
        }
    };

    // ========================================================
    // LE MODIFICATEUR ALÉATOIRE (règle "chaos")
    // ========================================================
    static readonly Random rngChaos = new Random();

    /// <summary>
    /// Tire un réglage au hasard et lui donne une valeur au hasard. Renvoie le texte à
    /// annoncer ("Gravité -> x2.15"), ou "" si rien n'a pu être tiré.
    /// Appelé UNIQUEMENT par l'hôte, qui rediffuse ensuite les règles à tout le monde.
    /// </summary>
    public static string TirerModificateurAleatoire(bool enLigne)
    {
        List<Regle> candidats = new List<Regle>();
        foreach (Regle r in Liste)
        {
            if (r.Cle == "chaos" || r.Type == Genre.Masque) continue; // on ne se coupe pas les armes
            if (enLigne && r.SoloSeulement) continue;                 // zombies : sans effet en ligne
            candidats.Add(r);
        }
        if (candidats.Count == 0) return "";

        Regle choisie = candidats[rngChaos.Next(candidats.Count)];
        int crans = Math.Max(1, (int)MathF.Round((choisie.Max - choisie.Min) / choisie.Pas));
        Poser(choisie, choisie.Min + rngChaos.Next(crans + 1) * choisie.Pas);
        return choisie.Nom + " -> " + choisie.Texte;
    }
}
