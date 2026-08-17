using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

// ========================================================
// LE SYSTÈME DE COSMÉTIQUES (Skins) — VERSION 2
// ========================================================
// - 9 couleurs de corps (primitives, comme avant)
// - CHAPEAUX : de vrais modèles 3D (.glb) scannés dans  assets/models/hat/
// - TÊTES    : des textures (.png) scannées dans        assets/textures/face/
//   peintes directement sur la surface de la tête (le dôme de la capsule).
//
// AJOUTER UN COSMÉTIQUE = déposer un fichier dans le bon dossier, c'est tout.
// L'ordre dans le menu = l'ordre alphabétique des fichiers ; préfixer
// "01-", "02-"... pour choisir l'ordre ("01-lunettes-soleil.glb" -> "Lunettes soleil").
//
// CONVENTION DES MODÈLES DE CHAPEAUX (export Blender -> .glb) :
// - 1 unité = 1 mètre, échelle 1:1 avec le jeu.
// - L'ORIGINE du modèle = le CENTRE du dôme de la tête (la demi-sphère de
//   rayon 0.5 au sommet de la capsule). Dans Blender : Z = le haut, -Y = devant
//   le joueur (l'export glTF convertit tout seul vers le repère du jeu : Y haut, +Z devant).
// - Le fichier assets/blender/build_hats.py reconstruit tous les chapeaux.
//
// ⚠ HITBOX : les chapeaux et visages sont PUREMENT VISUELS. Les tirs testent
// une box virtuelle FIXE autour de la capsule (Network.cs / Weapon.cs) : tirer
// sur un chapeau (même la grande flèche) ne touche JAMAIS le joueur.
//
// RÉSEAU : le skin voyage en indices (PlayerInfoPacket). Tous les clients d'une
// même version ont les mêmes fichiers donc les mêmes indices ; un indice hors
// liste (version différente) est simplement ignoré au dessin.
//
// ANATOMIE DU PERSONNAGE :
// - Le corps est une capsule (rayon 0.5, de centre-1 à centre+1 en Y).
// - La TÊTE est le dôme du haut (demi-sphère de rayon 0.5 centrée sur le
//   sommet du segment, à centre + 0.5).
partial class Program
{
    // --- LES 9 COULEURS DU CORPS (palette sobre, pas flashy) ---
    public static readonly string[] nomsCouleursSkin = { "Orange", "Rouge", "Bleu", "Vert", "Gris clair", "Violet", "Rose", "Marron", "Noir" };
    public static readonly Color[] couleursSkin =
    {
        new Color(185, 95, 15, 255),    // orange foncé
        new Color(140, 30, 30, 255),    // rouge foncé
        new Color(35, 60, 140, 255),    // bleu foncé
        new Color(30, 105, 50, 255),    // vert foncé
        new Color(185, 185, 185, 255),  // gris clair
        new Color(90, 45, 130, 255),    // violet foncé
        new Color(170, 70, 120, 255),   // rose foncé
        new Color(85, 55, 35, 255),     // marron foncé
        new Color(35, 35, 35, 255)      // noir
    };

    // --- LES CHAPEAUX : modèles 3D chargés depuis assets/models/hat/*.glb ---
    public class ChapeauSkin
    {
        public string Fichier = "";  // nom du fichier sans extension (sert d'identifiant dans skin.cfg)
        public string Nom = "";      // joli nom affiché dans le menu
        public Model Modele;
    }
    public static readonly List<ChapeauSkin> chapeauxSkin = new();

    // --- LES TÊTES : textures chargées depuis assets/textures/face/*.png ---
    public class FaceSkin
    {
        public string Fichier = "";
        public string Nom = "";
        public Texture2D Texture;
    }
    public static readonly List<FaceSkin> facesSkin = new();

    // Shader du visage : identique au shader par défaut mais avec un "discard"
    // des pixels transparents, pour qu'ils n'écrivent pas dans le tampon de
    // profondeur (sinon le contour invisible du visage pourrait masquer un
    // joueur ou un décor dessiné après).
    static Shader faceShader;

    // --- LE SKIN ACTUELLEMENT ÉQUIPÉ ---
    public static int skinCouleur = 0;  // index dans couleursSkin
    public static int skinHat = -1;     // index dans chapeauxSkin (-1 = aucun)
    public static int skinFace = -1;    // index dans facesSkin (-1 = tête lisse)

    // --- ÉTAT DU MENU DE PERSONNALISATION ---
    static int ongletCustom = 0;        // 0 = Couleur, 1 = Chapeau, 2 = Tête
    static float previewYaw = 0.6f;     // Rotation du perso dans l'aperçu (clic-glisser)

    const float RadVersDeg = 180f / MathF.PI;

    // ==========================================
    // CHARGEMENT DES COSMÉTIQUES (après InitWindow : il faut le contexte OpenGL)
    // ==========================================
    public static void ChargerCosmetiques()
    {
        // 1) LES CHAPEAUX (.glb)
        chapeauxSkin.Clear();
        const string dossierHats = "assets/models/hat";
        if (Directory.Exists(dossierHats))
        {
            string[] fichiers = Directory.GetFiles(dossierHats, "*.glb");
            Array.Sort(fichiers, StringComparer.OrdinalIgnoreCase);
            foreach (string f in fichiers)
            {
                Model m = Raylib.LoadModel(f);
                if (m.MeshCount == 0) continue; // fichier illisible -> ignoré
                chapeauxSkin.Add(new ChapeauSkin
                {
                    Fichier = Path.GetFileNameWithoutExtension(f),
                    Nom = NomCosmetique(f),
                    Modele = m
                });
            }
        }

        // 2) LES TÊTES (images)
        facesSkin.Clear();
        const string dossierFaces = "assets/textures/face";
        if (Directory.Exists(dossierFaces))
        {
            List<string> fichiers = new();
            foreach (string f in Directory.GetFiles(dossierFaces))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp") fichiers.Add(f);
            }
            fichiers.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string f in fichiers)
            {
                Image img = Raylib.LoadImage(f);
                if (img.Width == 0) continue;
                RendreBlancTransparent(ref img);
                Texture2D t = Raylib.LoadTextureFromImage(img);
                Raylib.UnloadImage(img);
                // Mipmaps + filtrage : sinon le visage scintille dès qu'on s'éloigne
                Raylib.GenTextureMipmaps(ref t);
                Raylib.SetTextureFilter(t, TextureFilter.Trilinear);
                facesSkin.Add(new FaceSkin
                {
                    Fichier = Path.GetFileNameWithoutExtension(f),
                    Nom = NomCosmetique(f),
                    Texture = t
                });
            }
        }

        // 3) Le shader du visage (vertex par défaut + fragment avec discard)
        faceShader = Raylib.LoadShader(null, "assets/shaders/face.fs");
    }

    // "02-chapeau-sorcier.glb" -> "Chapeau sorcier" (le préfixe numérique ne sert
    // qu'à ordonner les fichiers, les tirets deviennent des espaces).
    static string NomCosmetique(string chemin)
    {
        string nom = Path.GetFileNameWithoutExtension(chemin);
        int i = 0;
        while (i < nom.Length && char.IsDigit(nom[i])) i++;
        if (i > 0 && i < nom.Length && (nom[i] == '-' || nom[i] == '_' || nom[i] == ' ')) nom = nom[(i + 1)..];
        nom = nom.Replace('-', ' ').Replace('_', ' ').Trim();
        if (nom.Length > 0) nom = char.ToUpper(nom[0]) + nom[1..];
        return nom.Length > 0 ? nom : "Sans nom";
    }

    // CONVENTION "FOND BLANC = TRANSPARENT" : les visages sont souvent dessinés
    // sur fond blanc (Paint...). Si l'image n'a AUCUNE transparence, on rend les
    // pixels quasi blancs invisibles pour que seul le dessin se peigne sur la tête.
    // Une image qui possède déjà un canal alpha est laissée telle quelle.
    static void RendreBlancTransparent(ref Image img)
    {
        Raylib.ImageFormat(ref img, PixelFormat.UncompressedR8G8B8A8);
        unsafe
        {
            byte* px = (byte*)img.Data;
            int n = img.Width * img.Height;
            for (int i = 0; i < n; i++)
                if (px[i * 4 + 3] < 250) return; // il y a déjà de la transparence -> on respecte l'image

            // Couleur moyenne du dessin (les pixels qui vont rester opaques) : on la
            // met sous les pixels transparents. Sans ça, les mipmaps mélangeraient
            // les traits avec du BLANC et le dessin deviendrait pâle avec la distance.
            long r = 0, g = 0, b = 0, nb = 0;
            for (int i = 0; i < n; i++)
            {
                if (px[i * 4] >= 235 && px[i * 4 + 1] >= 235 && px[i * 4 + 2] >= 235) continue;
                r += px[i * 4]; g += px[i * 4 + 1]; b += px[i * 4 + 2]; nb++;
            }
            byte mr = (byte)(nb > 0 ? r / nb : 0), mg = (byte)(nb > 0 ? g / nb : 0), mb = (byte)(nb > 0 ? b / nb : 0);

            for (int i = 0; i < n; i++)
            {
                if (px[i * 4] >= 235 && px[i * 4 + 1] >= 235 && px[i * 4 + 2] >= 235)
                {
                    px[i * 4] = mr; px[i * 4 + 1] = mg; px[i * 4 + 2] = mb;
                    px[i * 4 + 3] = 0;
                }
            }
        }
    }

    // ==========================================
    // SAUVEGARDE / CHARGEMENT DU SKIN (skin.cfg)
    // On sauvegarde les NOMS de fichiers (pas les indices) : ajouter un nouveau
    // chapeau dans le dossier ne décale donc jamais le skin équipé.
    // ==========================================
    public static void ChargerSkin()
    {
        try
        {
            if (!File.Exists("skin.cfg")) return;
            string[] parts = File.ReadAllText("skin.cfg").Split(';');
            skinCouleur = Math.Clamp(int.Parse(parts[0]), 0, couleursSkin.Length - 1);
            skinHat = ChercherCosmetique(parts[1], chapeauxSkin.Count, s => chapeauxSkin[s].Fichier);
            skinFace = ChercherCosmetique(parts[2], facesSkin.Count, s => facesSkin[s].Fichier);
        }
        catch { /* fichier corrompu : on garde le skin par défaut */ }
    }

    // Retrouve un cosmétique par son nom de fichier ("-" = aucun). Les vieux
    // skin.cfg (indices numériques) restent acceptés.
    static int ChercherCosmetique(string id, int count, Func<int, string> fichier)
    {
        id = id.Trim();
        if (id.Length == 0 || id == "-") return -1;
        for (int i = 0; i < count; i++)
            if (string.Equals(fichier(i), id, StringComparison.OrdinalIgnoreCase)) return i;
        if (int.TryParse(id, out int legacy) && legacy >= 0 && legacy < count) return legacy;
        return -1;
    }

    public static void SauvegarderSkin()
    {
        try
        {
            string hat = (skinHat >= 0 && skinHat < chapeauxSkin.Count) ? chapeauxSkin[skinHat].Fichier : "-";
            string face = (skinFace >= 0 && skinFace < facesSkin.Count) ? facesSkin[skinFace].Fichier : "-";
            File.WriteAllText("skin.cfg", $"{skinCouleur};{hat};{face}");
        }
        catch { }
    }

    // ==========================================
    // LE DESSIN DU PERSONNAGE COMPLET
    // (Partagé entre l'aperçu du menu ET les joueurs distants en jeu)
    // ==========================================
    // 'lean' = penchement wall-run (le roll caméra du joueur). Purement visuel :
    // la capsule PHYSIQUE reste parfaitement droite (les capteurs de murs ne sont
    // donc jamais perturbés), seul le modèle 3D s'incline, plafonné à ±10°.
    public static void DessinerPersonnageComplet(Vector3 centre, float yaw, float pitch, int couleurIdx, int hatIdx, int faceIdx, float lean = 0f)
    {
        if (couleurIdx < 0 || couleurIdx >= couleursSkin.Length) couleurIdx = 0;

        Rlgl.PushMatrix();
        Rlgl.Translatef(centre.X, centre.Y, centre.Z);
        Rlgl.Rotatef(yaw * RadVersDeg, 0, 1, 0);
        // Dans ce repère local : +Z = devant le joueur, +X = sa droite

        // Penchement wall-run : on mappe le roll caméra (max ±0.25 rad) vers ±10° max
        float leanRad = Math.Clamp(lean * 0.7f, -0.1745f, 0.1745f);
        if (leanRad != 0f) Rlgl.Rotatef(leanRad * RadVersDeg, 0, 0, 1);

        // Le corps (lisse : pas de fil de fer, il salissait le visage peint dessus,
        // mais ombré pour qu'on lise bien le volume de la capsule)
        DessinerCapsuleOmbree(0.5f, -0.5f, 0.5f, 20, 8, couleursSkin[couleurIdx]);

        // === SYSTÈME DE CALQUES ===
        // 1) Le VISAGE d'abord (peint sur la tête)
        // 2) Le CHAPEAU (modèle 3D) par-dessus
        // Cet ordre garantit que le couvre-chef recouvre toujours le visage.
        DessinerFaceSkinLocale(faceIdx, pitch);
        DessinerChapeauSkinLocal(hatIdx);

        Rlgl.PopMatrix();
    }

    // ==========================================
    // LE CORPS — une capsule OMBRÉE, faite à la main.
    // Les joueurs sont dessinés SANS shader d'éclairage (contrairement à la map) :
    // un Raylib.DrawCapsule donne donc un aplat de couleur où l'on ne distingue
    // plus le volume. On génère la géométrie nous-mêmes et on cuit un lambert
    // dans la couleur de chaque sommet — exactement ce que build_hats.py fait pour
    // les chapeaux, avec la MÊME lumière : le corps et le chapeau s'éclairent pareil.
    // ==========================================
    // La lumière vit dans le repère LOCAL du personnage (elle tourne donc avec lui,
    // comme celle cuite dans les .glb) : elle vient d'en haut, devant, à droite.
    // Blender (0.4, -0.55, 0.73) -> jeu (0.4, 0.73, 0.55).
    static readonly Vector3 LUMIERE_SKIN = Vector3.Normalize(new Vector3(0.40f, 0.73f, 0.55f));

    // Le même dégradé que build_hats.py : une base ambiante généreuse (le perso ne
    // doit jamais devenir noir) + le lambert + un soupçon de ciel sur le dessus.
    static float OmbrageSkin(Vector3 normale)
        => MathF.Min(1f, 0.60f + 0.40f * MathF.Max(0f, Vector3.Dot(normale, LUMIERE_SKIN))
                              + 0.07f * MathF.Max(0f, normale.Y));

    // Capsule verticale de rayon 'rayon', dont les centres des dômes sont à yHaut
    // et yBas dans le repère courant.
    static void DessinerCapsuleOmbree(float yHaut, float yBas, float rayon, int slices, int rings, Color couleur)
    {
        // La texture blanche par défaut : ce sont les couleurs de sommets qui décident
        // de tout (et ça évite d'hériter d'une texture laissée par le dessin précédent).
        Rlgl.SetTexture(0);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.TexCoord2f(0f, 0f);

        // Les latitudes : l'hémisphère du BAS (centré sur yBas) puis celui du HAUT
        // (centré sur yHaut). Les deux rangées de l'équateur (phi=0) ont la même
        // normale horizontale mais des Y différents : le "saut" entre elles forme
        // tout seul le flanc cylindrique du corps.
        int nbRangees = 2 * (rings + 1);
        for (int r = 0; r < nbRangees - 1; r++)
        {
            for (int s = 0; s < slices; s++)
            {
                float t0 = s * MathF.Tau / slices;
                float t1 = (s + 1) * MathF.Tau / slices;
                // Ordre anti-horaire vu de l'extérieur (sinon le culling mange le corps)
                SommetCapsule(t0, r, rings, yHaut, yBas, rayon, couleur);
                SommetCapsule(t1, r, rings, yHaut, yBas, rayon, couleur);
                SommetCapsule(t1, r + 1, rings, yHaut, yBas, rayon, couleur);
                SommetCapsule(t0, r + 1, rings, yHaut, yBas, rayon, couleur);
            }
        }

        Rlgl.End();
    }

    static void SommetCapsule(float theta, int rangee, int rings, float yHaut, float yBas, float rayon, Color couleur)
    {
        bool basse = rangee <= rings;
        float phi = basse ? -MathF.PI / 2f + (MathF.PI / 2f) * rangee / rings   // -90° -> 0°
                          : (MathF.PI / 2f) * (rangee - rings - 1) / rings;     //   0° -> +90°
        float centreY = basse ? yBas : yHaut;

        Vector3 n = new(MathF.Cos(phi) * MathF.Sin(theta), MathF.Sin(phi), MathF.Cos(phi) * MathF.Cos(theta));
        float s = OmbrageSkin(n);

        Rlgl.Color4ub((byte)(couleur.R * s), (byte)(couleur.G * s), (byte)(couleur.B * s), couleur.A);
        Rlgl.Vertex3f(n.X * rayon, centreY + n.Y * rayon, n.Z * rayon);
    }

    // ==========================================
    // LE VISAGE — une TEXTURE peinte sur la tête, comme un tatouage.
    // On construit un petit "patch" incurvé (grille de quads) qui épouse
    // exactement la surface de la capsule :
    //   - au-dessus du centre du dôme : la sphère (rayon 0.5)
    //   - en dessous : le cylindre du corps (rayon 0.5)
    // Le patch glisse sur la surface en suivant le regard (pitch), comme avant.
    // ==========================================
    // Le patch est ~carré sur la surface (largeur 2*THETA ≈ hauteur HAUT-BAS) pour
    // ne pas déformer les dessins, et centré LÉGÈREMENT au-dessus de l'équateur du
    // dôme : posé comme un visage, pas comme une casquette.
    const int FACE_GRILLE = 10;          // grille 10x10 quads (assez lisse pour la courbure)
    const float FACE_THETA = 0.55f;      // demi-largeur angulaire (~31° de chaque côté)
    const float FACE_PHI_HAUT = 0.62f;   // le haut du dessin (~36° au-dessus de l'équateur)
    const float FACE_PHI_BAS = -0.42f;   // le bas du dessin (~-24°, juste sous le dôme)

    static void DessinerFaceSkinLocale(int faceIdx, float pitch)
    {
        if (faceIdx < 0 || faceIdx >= facesSkin.Count) return;
        Texture2D tex = facesSkin[faceIdx].Texture;

        Rlgl.PushMatrix();
        Rlgl.Translatef(0, 0.5f, 0); // origine = centre du dôme (la vraie tête)

        // Le visage suit le regard mais en AMORTI : à fond vers le haut, un visage
        // qui suivrait le pitch complet basculerait derrière le crâne et deviendrait
        // invisible de face. Amorti, il "pointe" la direction sans jamais disparaître.
        float pitchFace = Math.Clamp(pitch * 0.6f, -0.55f, 0.55f);
        float cosP = MathF.Cos(pitchFace);
        float sinP = MathF.Sin(pitchFace);

        Raylib.BeginShaderMode(faceShader);
        Rlgl.SetTexture(tex.Id);
        Rlgl.Begin(DrawMode.Quads);

        for (int iy = 0; iy < FACE_GRILLE; iy++)
        {
            for (int ix = 0; ix < FACE_GRILLE; ix++)
            {
                float u0 = ix / (float)FACE_GRILLE, u1 = (ix + 1) / (float)FACE_GRILLE;
                float v0 = iy / (float)FACE_GRILLE, v1 = (iy + 1) / (float)FACE_GRILLE;
                // Ordre anti-horaire vu de face (sinon le backface culling mange le visage) :
                CoinVisage(u0, v0, cosP, sinP);
                CoinVisage(u0, v1, cosP, sinP);
                CoinVisage(u1, v1, cosP, sinP);
                CoinVisage(u1, v0, cosP, sinP);
            }
        }

        Rlgl.End();
        Rlgl.SetTexture(0);
        Raylib.EndShaderMode();
        Rlgl.PopMatrix();
    }

    // Un coin de quad du visage : (u,v) de la texture -> point sur la capsule.
    static void CoinVisage(float u, float v, float cosP, float sinP)
    {
        // u=0 -> la gauche de l'image apparaît à la gauche du SPECTATEUR qui regarde le joueur
        float theta = -FACE_THETA + 2f * FACE_THETA * u;
        float phi = FACE_PHI_HAUT + (FACE_PHI_BAS - FACE_PHI_HAUT) * v; // v=0 = le haut de l'image

        // Direction sur la sphère unité, puis rotation "regard" autour de X
        Vector3 d = new(MathF.Sin(theta) * MathF.Cos(phi), MathF.Sin(phi), MathF.Cos(theta) * MathF.Cos(phi));
        d = new Vector3(d.X, d.Y * cosP + d.Z * sinP, d.Z * cosP - d.Y * sinP);

        Vector3 p, normale;
        if (d.Y >= 0f)
        {
            // Au-dessus de l'équateur du dôme : on se pose sur la SPHÈRE
            p = d * 0.5f;
            normale = d;
        }
        else
        {
            // En dessous : on se pose sur le CYLINDRE du corps (rayon 0.5).
            // Le Max() évite l'explosion du t quand la direction pique vers le bas.
            float horiz = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
            float t = 0.5f / MathF.Max(horiz, 0.30f);
            p = d * t;
            if (p.Y < -0.9f) p.Y = -0.9f; // on reste sur le haut du corps
            normale = Vector3.Normalize(new Vector3(p.X, 0f, p.Z));
        }

        p += normale * 0.008f; // léger décollement anti z-fighting avec la capsule

        // Le visage prend le MÊME ombrage que la capsule sous lui : sans ça, le
        // dessin resterait plein feux sur un corps ombré et flotterait au-dessus.
        float s = OmbrageSkin(normale);

        Rlgl.Color4ub((byte)(255 * s), (byte)(255 * s), (byte)(255 * s), 255);
        Rlgl.TexCoord2f(u, v);
        Rlgl.Vertex3f(p.X, p.Y, p.Z);
    }

    // ==========================================
    // LE CHAPEAU — un modèle 3D posé sur le crâne.
    // Les .glb sont exportés avec l'origine AU CENTRE DU DÔME et l'avant vers +Z :
    // il suffit donc de dessiner le modèle à l'origine du repère de la tête.
    // Comme un vrai chapeau, il suit le yaw mais PAS le pitch.
    // ==========================================
    static void DessinerChapeauSkinLocal(int hatIdx)
    {
        if (hatIdx < 0 || hatIdx >= chapeauxSkin.Count) return;

        Rlgl.PushMatrix();
        Rlgl.Translatef(0, 0.5f, 0); // le centre du dôme
        Raylib.DrawModel(chapeauxSkin[hatIdx].Modele, Vector3.Zero, 1f, Color.White);
        Rlgl.PopMatrix();
    }

    // ==========================================
    // LE MENU DE PERSONNALISATION (état Customization)
    // ==========================================
    public static void MenuCustomization()
    {
        // ClearBackground nettoie AUSSI le tampon de profondeur : indispensable
        // pour que l'aperçu 3D ne se batte pas avec la frame précédente.
        Raylib.ClearBackground(new Color(240, 240, 240, 255));
        Raylib.DrawTextureEx(BlurBackground, new Vector2(0, 0), 0f, 1f, Color.White);

        Vector2 souris = Raylib.GetMousePosition();

        // --- ROTATION DU PERSO AU CLIC-GLISSER (sur la moitié gauche de l'écran) ---
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && souris.X < LargeurFenetre / 2f && souris.Y > 130)
        {
            previewYaw += Raylib.GetMouseDelta().X * 0.012f;
        }

        // --- L'APERÇU 3D DU PERSONNAGE ---
        // (Le Target est décalé en X pour que le perso apparaisse sur la gauche de l'écran)
        Camera3D camPreview = new Camera3D
        {
            Position = new Vector3(0.9f, 1.15f, 3.4f),
            Target = new Vector3(0.9f, 0.75f, 0f),
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };

        Raylib.BeginMode3D(camPreview);
        Raylib.DrawCylinder(new Vector3(0, -0.55f, 0), 1.0f, 1.1f, 0.12f, 24, new Color(70, 70, 70, 255)); // le socle
        DessinerPersonnageComplet(new Vector3(0, 0.6f, 0), previewYaw, 0f, skinCouleur, skinHat, skinFace);
        Raylib.EndMode3D();

        // --- TITRE & AIDE ---
        Raylib.DrawText("PERSONNALISATION", LargeurFenetre / 2 - Raylib.MeasureText("PERSONNALISATION", 40) / 2, 60, 40, Color.Black);
        Raylib.DrawText("Clique et glisse sur le personnage pour le faire tourner", 120, HauteurFenetre - 80, 20, Color.DarkGray);

        // --- LES 3 ONGLETS ---
        int panelX = LargeurFenetre / 2 + 80;
        int tabY = 170;
        string[] onglets = { "COULEUR", "CHAPEAU", "TETE" };
        for (int i = 0; i < onglets.Length; i++)
        {
            int tabX = panelX + i * 170;
            if (DrawButton(tabX, tabY, 160, 50, onglets[i]))
            {
                PlaySoundWithPriority(select, SoundPriority.Low);
                ongletCustom = i;
                // Valider une catégorie envoie le focus sur son 1er élément (couleur/chapeau/tête),
                // pour pouvoir choisir directement à la flèche du bas.
                MenuNav.SetFocus(onglets.Length);
            }
            if (ongletCustom == i) Raylib.DrawRectangle(tabX, tabY + 52, 160, 6, Color.Red);
        }

        int itemY = 260;

        if (ongletCustom == 0)
        {
            // --- LA GRILLE 3x3 DE COULEURS ---
            for (int i = 0; i < couleursSkin.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Rectangle swatch = new Rectangle(panelX + col * 110, itemY + row * 110, 90, 90);
                Raylib.DrawRectangleRec(swatch, couleursSkin[i]);

                bool hover = MenuNav.Item(swatch, out bool clicSwatch);
                bool equipee = (skinCouleur == i);
                Raylib.DrawRectangleLinesEx(swatch, equipee ? 6 : 2, equipee ? Color.Lime : (hover ? Color.Red : Color.Black));

                if (clicSwatch)
                {
                    PlaySoundWithPriority(select, SoundPriority.Low);
                    skinCouleur = i;
                }
            }
            Raylib.DrawText("Couleur : " + nomsCouleursSkin[skinCouleur], panelX, itemY + 3 * 110 + 10, 25, Color.Black);
        }
        else if (ongletCustom == 1)
        {
            // --- LA LISTE DES CHAPEAUX (les .glb trouvés dans assets/models/hat) ---
            if (chapeauxSkin.Count == 0)
            {
                Raylib.DrawText("Aucun chapeau trouve !", panelX, itemY, 25, Color.Maroon);
                Raylib.DrawText("Depose des fichiers .glb dans assets/models/hat/", panelX, itemY + 35, 20, Color.DarkGray);
            }
            for (int i = 0; i < chapeauxSkin.Count; i++)
            {
                int y = itemY + i * 75;
                bool estEquipe = (skinHat == i);

                if (DrawButton(panelX, y, 420, 60, (estEquipe ? "[X] " : "") + chapeauxSkin[i].Nom))
                {
                    PlaySoundWithPriority(select, SoundPriority.Low);
                    skinHat = estEquipe ? -1 : i; // re-cliquer sur l'objet équipé = le déséquiper
                }
                if (estEquipe) Raylib.DrawRectangleLinesEx(new Rectangle(panelX - 4, y - 4, 428, 68), 4, Color.Lime);
            }
            if (chapeauxSkin.Count > 0)
                Raylib.DrawText("Clique sur l'objet equipe [X] pour l'enlever", panelX, itemY + chapeauxSkin.Count * 75 + 10, 18, Color.DarkGray);
        }
        else
        {
            // --- LA GRILLE DES TÊTES : une vignette par texture trouvée ---
            if (facesSkin.Count == 0)
            {
                Raylib.DrawText("Aucune tete trouvee !", panelX, itemY, 25, Color.Maroon);
                Raylib.DrawText("Depose des images .png dans assets/textures/face/", panelX, itemY + 35, 20, Color.DarkGray);
            }
            const int colonnes = 4, cell = 100, pas = 112;
            for (int i = 0; i < facesSkin.Count; i++)
            {
                int col = i % colonnes;
                int row = i / colonnes;
                Rectangle caseTex = new Rectangle(panelX + col * pas, itemY + row * pas, cell, cell);

                // Fond clair pour bien voir les dessins fins, puis la vignette (aspect conservé)
                Raylib.DrawRectangleRec(caseTex, new Color(250, 250, 250, 255));
                Texture2D t = facesSkin[i].Texture;
                float echelle = MathF.Min((cell - 8) / (float)t.Width, (cell - 8) / (float)t.Height);
                float w = t.Width * echelle, h = t.Height * echelle;
                Raylib.DrawTexturePro(t,
                    new Rectangle(0, 0, t.Width, t.Height),
                    new Rectangle(caseTex.X + (cell - w) / 2f, caseTex.Y + (cell - h) / 2f, w, h),
                    Vector2.Zero, 0f, Color.White);

                bool hover = MenuNav.Item(caseTex, out bool clicFace);
                bool estEquipee = (skinFace == i);
                Raylib.DrawRectangleLinesEx(caseTex, estEquipee ? 6 : 2, estEquipee ? Color.Lime : (hover ? Color.Red : Color.Black));

                if (clicFace)
                {
                    PlaySoundWithPriority(select, SoundPriority.Low);
                    skinFace = estEquipee ? -1 : i; // re-cliquer = déséquiper
                }
            }
            if (facesSkin.Count > 0)
            {
                int basGrille = itemY + ((facesSkin.Count + colonnes - 1) / colonnes) * pas + 10;
                Raylib.DrawText("Tete : " + (skinFace >= 0 ? facesSkin[skinFace].Nom : "aucune (re-clique pour enlever)"), panelX, basGrille, 22, Color.Black);
            }
        }

        // --- RETOUR (avec sauvegarde du skin) — bouton, ou Échap / B ---
        if (DrawButton(50, 50, 150, 60, "RETOUR") || MenuNav.Back)
        {
            PlaySoundWithPriority(unselect, SoundPriority.Low);
            SauvegarderSkin();
            currentState = GameState.ModeSelection;
        }
    }

    // ==========================================
    // TEST VISUEL AUTOMATIQUE :  dotnet run -- --skintest [filtre]
    // Aligne un personnage par chapeau et un par visage, et exporte
    // skintest-hats.png / skintest-faces.png, puis quitte.
    // Avec un filtre (ex: --skintest robot), exporte EN PLUS skintest-zoom.png :
    // un gros plan du chapeau correspondant, vu de face, de 3/4 et de profil —
    // c'est ce qu'il faut regarder pour juger un modèle qu'on vient de sculpter.
    // ==========================================
    public static void LancerSkinTest(string filtre = "")
    {
        Raylib.InitWindow(1280, 720, "GoofyFPS - SkinTest");
        Raylib.SetTargetFPS(60);
        ChargerCosmetiques();

        // On dessine dans une RenderTexture (et pas TakeScreenshot) : le résultat
        // est identique quel que soit le scaling DPI de l'écran de la machine.
        RenderTexture2D rt = Raylib.LoadRenderTexture(1760, 900);

        // --- VUE 1 : un personnage par CHAPEAU ---
        Camera3D camHats = new Camera3D
        {
            Position = new Vector3(0f, 2.6f, 9.2f),
            Target = new Vector3(0f, 0.9f, 0f),
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };
        float espH = 2.1f;
        float x0 = -espH * (chapeauxSkin.Count - 1) / 2f;

        Raylib.BeginTextureMode(rt);
        Raylib.ClearBackground(new Color(150, 195, 235, 255));
        Raylib.BeginMode3D(camHats);
        Raylib.DrawPlane(new Vector3(0, -1f, 0), new Vector2(60, 60), new Color(110, 130, 110, 255));
        for (int i = 0; i < chapeauxSkin.Count; i++)
            DessinerPersonnageComplet(new Vector3(x0 + i * espH, 0f, 0f), 0.35f, 0f, i % couleursSkin.Length, i, -1);
        Raylib.EndMode3D();
        for (int i = 0; i < chapeauxSkin.Count; i++)
        {
            Vector2 e = Raylib.GetWorldToScreenEx(new Vector3(x0 + i * espH, 2.7f, 0f), camHats, 1760, 900);
            Raylib.DrawText(chapeauxSkin[i].Nom, (int)e.X - Raylib.MeasureText(chapeauxSkin[i].Nom, 18) / 2, (int)e.Y, 18, Color.Black);
        }
        Raylib.DrawText($"SKINTEST CHAPEAUX : {chapeauxSkin.Count} modeles", 20, 20, 26, Color.Black);
        Raylib.EndTextureMode();
        ExporterRenderTexture(rt, "skintest-hats.png");

        // --- VUE 2 : gros plan sur les VISAGES (3 pitchs pour voir le glissement) ---
        Camera3D camFaces = new Camera3D
        {
            Position = new Vector3(0f, 1.0f, 4.6f),
            Target = new Vector3(0f, 0.45f, 0f),
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };
        Raylib.BeginTextureMode(rt);
        Raylib.ClearBackground(new Color(150, 195, 235, 255));
        Raylib.BeginMode3D(camFaces);
        Raylib.DrawPlane(new Vector3(0, -1f, 0), new Vector2(60, 60), new Color(110, 130, 110, 255));
        int nbF = Math.Max(facesSkin.Count, 1);
        for (int i = 0; i < facesSkin.Count; i++)
        {
            float xc = (i - (nbF - 1) / 2f) * 5.2f;
            // le même visage 3 fois : regard bas / droit / haut (corps gris clair : le plus lisible)
            DessinerPersonnageComplet(new Vector3(xc - 1.6f, 0f, 0f), 0f, -0.6f, 4, -1, i);
            DessinerPersonnageComplet(new Vector3(xc, 0f, 0f), 0f, 0f, 4, -1, i);
            DessinerPersonnageComplet(new Vector3(xc + 1.6f, 0f, 0f), 0f, 0.6f, 4, -1, i);
        }
        Raylib.EndMode3D();
        Raylib.DrawText($"SKINTEST VISAGES : {facesSkin.Count} textures (pitch bas / droit / haut)", 20, 20, 26, Color.Black);
        Raylib.EndTextureMode();
        ExporterRenderTexture(rt, "skintest-faces.png");

        // --- VUE 3 (optionnelle) : le gros plan d'un chapeau, sous 3 angles ---
        int zoom = -1;
        if (filtre.Length > 0)
        {
            for (int i = 0; i < chapeauxSkin.Count && zoom < 0; i++)
                if (chapeauxSkin[i].Fichier.Contains(filtre, StringComparison.OrdinalIgnoreCase)) zoom = i;
        }
        if (zoom >= 0)
        {
            Camera3D camZoom = new Camera3D
            {
                Position = new Vector3(0f, 1.15f, 3.3f),
                Target = new Vector3(0f, 0.62f, 0f),
                Up = new Vector3(0, 1, 0),
                FovY = 45f,
                Projection = CameraProjection.Perspective
            };
            Raylib.BeginTextureMode(rt);
            Raylib.ClearBackground(new Color(150, 195, 235, 255));
            Raylib.BeginMode3D(camZoom);
            Raylib.DrawPlane(new Vector3(0, -1f, 0), new Vector2(60, 60), new Color(110, 130, 110, 255));
            // de face, de 3/4, de profil (corps gris clair : le plus neutre)
            DessinerPersonnageComplet(new Vector3(-1.35f, 0f, 0f), 0f, 0f, 4, zoom, -1);
            DessinerPersonnageComplet(new Vector3(0f, 0f, 0f), 0.9f, 0f, 4, zoom, -1);
            DessinerPersonnageComplet(new Vector3(1.35f, 0f, 0f), MathF.PI / 2f, 0f, 4, zoom, -1);
            Raylib.EndMode3D();
            Raylib.DrawText($"ZOOM : {chapeauxSkin[zoom].Nom}  (face / 3-4 / profil)", 20, 20, 26, Color.Black);
            Raylib.EndTextureMode();
            ExporterRenderTexture(rt, "skintest-zoom.png");
        }
        else if (filtre.Length > 0)
        {
            Console.WriteLine($"[SKINTEST] aucun chapeau ne contient \"{filtre}\" : pas de gros plan.");
        }

        Raylib.UnloadRenderTexture(rt);
        Raylib.CloseWindow();
        Console.WriteLine($"[SKINTEST] {chapeauxSkin.Count} chapeaux, {facesSkin.Count} visages -> skintest-hats.png / skintest-faces.png"
                        + (zoom >= 0 ? " / skintest-zoom.png" : ""));
    }

    static void ExporterRenderTexture(RenderTexture2D rt, string fichier)
    {
        Image img = Raylib.LoadImageFromTexture(rt.Texture);
        Raylib.ImageFlipVertical(ref img); // les RenderTextures OpenGL sont à l'envers
        Raylib.ExportImage(img, fichier);
        Raylib.UnloadImage(img);
    }
}
