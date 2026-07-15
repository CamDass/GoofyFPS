using System;
using System.Numerics;
using Raylib_cs;

/// <summary>
/// Navigation de menu unifiée : SOURIS + CLAVIER (flèches / Entrée / Échap) + MANETTE.
///
/// Deux principes :
///  1) MODE D'ENTRÉE. Dès qu'on bouge la souris, on repasse en mode "souris" et le
///     curseur réapparaît. Dès qu'on navigue au clavier ou à la manette, on passe en
///     mode "focus" et le curseur disparaît.
///  2) FOCUS IMMÉDIAT. Chaque bouton s'enregistre via Item() dans son ordre de dessin.
///     Le bouton focalisé est renvoyé comme "actif" et s'affiche donc EXACTEMENT comme
///     s'il était survolé par la souris (on réutilise le visuel de survol existant).
///
/// Utilisation dans un écran :
///     MenuNav.Begin(idEcran);            // une seule fois par frame, avant de dessiner
///     ... pour chaque bouton :
///         bool actif = MenuNav.Item(box, out bool declenche);
///         // dessiner "box" en style survolé si 'actif', et exécuter l'action si 'declenche'
///     MenuNav.End();
/// </summary>
public static class MenuNav
{
    // true = souris (curseur visible) ; false = clavier/manette (curseur caché)
    public static bool UsingMouse = true;
    static Vector2 lastMouse;

    // Fronts de navigation, évalués UNE SEULE fois par frame.
    // (IsMenuUp/DownPressed possèdent un "latch" de stick : les appeler deux fois par
    //  frame casserait la détection du front. On mémorise donc les valeurs ici.)
    public static bool Up, Down, Left, Right, Confirm, Back;

    // Vrai quand on est en train d'écrire dans un champ de texte : la navigation
    // (flèches/stick) est alors gelée pour ne pas gêner la saisie ; seuls Entrée/Échap
    // restent actifs (pour sortir du champ). Les écrans le remettent à jour chaque frame.
    public static bool EditingText;

    static int focusId;                 // bouton actuellement focalisé (ordre de dessin)
    static int nextId;                  // compteur d'items pendant la frame en cours
    static int countLast;               // nombre d'items de la frame précédente (pour le wrap)
    static int lastScreenId = int.MinValue;

    /// <summary>
    /// À appeler UNE fois par frame, avant de dessiner l'écran de menu courant.
    /// verticalOnly = true : le focus ne bouge qu'avec Haut/Bas et laisse Gauche/Droite
    /// disponibles à l'écran (ex. régler un curseur/slider dans les Options).
    /// </summary>
    public static void Begin(int screenId, bool verticalOnly = false)
    {
        // 1. Évaluer la navigation une seule fois (sinon le latch du stick casse).
        Up      = KeyBinds.IsMenuUpPressed();
        Down    = KeyBinds.IsMenuDownPressed();
        Left    = KeyBinds.IsMenuLeftPressed();
        Right   = KeyBinds.IsMenuRightPressed();
        Confirm = KeyBinds.IsMenuConfirmPressed();
        Back    = KeyBinds.IsMenuBackPressed();

        // 2. Changement d'écran -> on remet le focus sur le premier bouton (et on sort de toute saisie).
        if (screenId != lastScreenId) { focusId = 0; EditingText = false; lastScreenId = screenId; }

        // 3. Détection du device actif -> visibilité du curseur.
        Vector2 m = Raylib.GetMousePosition();
        bool mouseMoved = Vector2.Distance(m, lastMouse) > 2f
                          || Raylib.IsMouseButtonPressed(MouseButton.Left)
                          || Raylib.IsMouseButtonPressed(MouseButton.Right);
        lastMouse = m;
        if (mouseMoved) UsingMouse = true;
        else if (Up || Down || Left || Right || Confirm) UsingMouse = false;

        // Curseur visible en mode souris, caché en mode clavier/manette.
        if (UsingMouse) Raylib.ShowCursor(); else Raylib.HideCursor();

        // 4. Déplacer le focus (navigation en boucle dans l'ordre de dessin).
        //    Gelé pendant la saisie d'un champ de texte (valeur posée par la frame précédente).
        if (!EditingText && !UsingMouse && countLast > 0)
        {
            bool suivant   = Down || (!verticalOnly && Right);
            bool precedent = Up   || (!verticalOnly && Left);
            if (suivant)   focusId = (focusId + 1) % countLast;
            if (precedent) focusId = (focusId % countLast + countLast - 1) % countLast;
        }
        // On remet le drapeau à zéro : un champ de texte en cours d'édition le rallumera
        // pendant cette frame (et gèlera donc la nav à la frame suivante).
        EditingText = false;

        nextId = 0;
    }

    /// <summary>À appeler après avoir dessiné tous les items de l'écran.</summary>
    public static void End()
    {
        countLast = nextId;
        if (focusId >= countLast) focusId = Math.Max(0, countLast - 1);
    }

    /// <summary>
    /// Enregistre un bouton cliquable. Renvoie true s'il est "actif" (survolé à la souris
    /// OU focalisé au clavier/manette) : à utiliser pour l'affichage en style survolé.
    /// 'activated' vaut true à la frame où l'action doit se déclencher (relâchement du clic
    /// souris OU Entrée / bouton A quand il est focalisé).
    /// </summary>
    public static bool Item(Rectangle box, out bool activated)
    {
        int id = nextId++;
        bool mouseOver = UsingMouse && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), box);
        if (mouseOver) focusId = id;                     // la souris synchronise le focus
        bool focused = mouseOver || (!UsingMouse && id == focusId);
        activated = (mouseOver && Raylib.IsMouseButtonReleased(MouseButton.Left))
                    || (focused && !UsingMouse && Confirm);
        return focused;
    }

    /// <summary>Variante sans le flag d'activation (pour un item purement visuel/focalisable).</summary>
    public static bool Item(Rectangle box) => Item(box, out _);

    /// <summary>Nombre d'items enregistrés jusqu'ici dans la frame courante (= id du prochain Item).</summary>
    public static int NextId => nextId;

    /// <summary>Force le focus sur l'item d'index donné (ex. entrer dans une catégorie).</summary>
    public static void SetFocus(int id)
    {
        focusId = id;
        UsingMouse = false; // on passe en mode focus pour voir la sélection
    }

    /// <summary>Force le mode manette/clavier (curseur caché) — ex. en entrant dans un menu.</summary>
    public static void ForceKeyboardMode()
    {
        UsingMouse = false;
        Raylib.HideCursor();
    }
}
