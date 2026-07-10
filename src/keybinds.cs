using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

/// <summary>
/// Couche d'entrée unifiée : chaque action de jeu est interrogée via une méthode
/// sémantique (IsJumpingPressed, IsShootingHeld, ...) qui fusionne le CLAVIER/SOURIS
/// et la MANETTE. Le code du jeu appelle ces helpers, donc une même action fonctionne
/// indifféremment au clavier ou à la manette.
///
/// ================= MAPPETTE MANETTE — INSPIRÉE D'APEX LEGENDS (schéma Xbox) =================
///   Stick gauche ............. Déplacement (ZQSD/WASD)
///   Stick droit .............. Caméra / visée (souris)
///   L3 (clic stick gauche) ... Sprint
///   RT (gâchette droite) ..... Tirer (clic gauche)
///   LT (gâchette gauche) ..... Viser / ADS (clic droit)
///   A (croix, bas) ........... Sauter (Espace)
///   B (rond, droite) ......... S'accroupir / glissade (C)
///   X (carré, gauche) ........ Recharger (R)
///   LB (bumper gauche) ....... Construire un mur (maintenir), puis RT pour poser (F + clic)
///   RB (bumper droit) ........ Dash (Ctrl)
///   Start / Menu ............. Pause (Tab)
/// ============================================================================================
/// </summary>
public static class KeyBinds
{
    // ======================================================
    // CONFIGURATION MANETTE
    // ======================================================
    public const int GamepadIndex = 0;      // Première manette branchée
    const float StickDeadzone = 0.15f;      // Zone morte des sticks (anti-drift)
    const float MoveThreshold = 0.35f;      // Poussée mini du stick gauche pour "marcher"

    /// <summary>Vrai si une manette est branchée et détectée par Raylib.</summary>
    public static bool GamepadConnected()
    {
        return Raylib.IsGamepadAvailable(GamepadIndex);
    }

    // --- petits utilitaires internes (rendent false/0 si aucune manette) ---
    static float Axis(GamepadAxis axis)
    {
        if (!GamepadConnected()) return 0f;
        return Raylib.GetGamepadAxisMovement(GamepadIndex, axis);
    }
    static bool BtnDown(GamepadButton b)
    {
        if (!GamepadConnected()) return false;
        return Raylib.IsGamepadButtonDown(GamepadIndex, b);
    }
    static bool BtnPressed(GamepadButton b)
    {
        if (!GamepadConnected()) return false;
        return Raylib.IsGamepadButtonPressed(GamepadIndex, b);
    }

    // ===== HELPER METHODS (clavier/souris + manette) =====

    // Déplacement : touches OU stick gauche poussé au-delà du seuil.
    // (Le stick reste "tout ou rien" par direction : la vitesse est ensuite normalisée
    //  comme au clavier, ce qui préserve intact le slide / wallrun / bunny-hop.)
    public static bool IsMoveForwardPressed()  => Raylib.IsKeyDown(Settings.KEY_MoveForward)  || Raylib.IsKeyDown(Settings.KEY_MoveForwardAlt)  || Axis(GamepadAxis.LeftY) < -MoveThreshold;
    public static bool IsMoveBackwardPressed() => Raylib.IsKeyDown(Settings.KEY_MoveBackward) || Raylib.IsKeyDown(Settings.KEY_MoveBackwardAlt) || Axis(GamepadAxis.LeftY) >  MoveThreshold;
    public static bool IsMoveLeftPressed()     => Raylib.IsKeyDown(Settings.KEY_MoveLeft)     || Raylib.IsKeyDown(Settings.KEY_MoveLeftAlt)     || Axis(GamepadAxis.LeftX) < -MoveThreshold;
    public static bool IsMoveRightPressed()    => Raylib.IsKeyDown(Settings.KEY_MoveRight)    || Raylib.IsKeyDown(Settings.KEY_MoveRightAlt)    || Axis(GamepadAxis.LeftX) >  MoveThreshold;

    public static bool IsSprintingPressed() => Raylib.IsKeyDown(Settings.KEY_Sprint)   || BtnDown(GamepadButton.LeftThumb);       // L3
    public static bool IsCrouchingPressed() => Raylib.IsKeyDown(Settings.KEY_Crouch)   || BtnDown(GamepadButton.RightFaceRight);  // B / rond
    public static bool IsBuildWallPressed() => Raylib.IsKeyDown(Settings.KEY_BuildWall)|| BtnDown(GamepadButton.LeftTrigger1);     // LB

    public static bool IsJumpingPressed()   => Raylib.IsKeyPressed(Settings.KEY_Jump)   || BtnPressed(GamepadButton.RightFaceDown);  // A / croix (front montant)
    public static bool IsJumpHeld()         => Raylib.IsKeyDown(Settings.KEY_Jump)      || BtnDown(GamepadButton.RightFaceDown);     // A maintenu
    public static bool IsDashingPressed()   => Raylib.IsKeyPressed(Settings.KEY_Dash)   || BtnPressed(GamepadButton.RightTrigger1);  // RB
    public static bool IsReloadingPressed() => Raylib.IsKeyPressed(Settings.KEY_Reload) || BtnPressed(GamepadButton.RightFaceLeft);  // X / carré
    public static bool IsCrouchPressedEdge()=> Raylib.IsKeyPressed(Settings.KEY_Crouch) || BtnPressed(GamepadButton.RightFaceRight); // B (front montant, pour le boost de glissade)

    // Combat : souris OU gâchettes
    public static bool IsAimingHeld()      => Raylib.IsMouseButtonDown(MouseButton.Right)    || BtnDown(GamepadButton.LeftTrigger2);    // LT
    public static bool IsShootingHeld()    => Raylib.IsMouseButtonDown(MouseButton.Left)     || BtnDown(GamepadButton.RightTrigger2);   // RT
    public static bool IsShootingPressed() => Raylib.IsMouseButtonPressed(MouseButton.Left)  || BtnPressed(GamepadButton.RightTrigger2);

    // Menus
    // TAB (touche configurable) OU ÉCHAP mettent en pause, comme dans la plupart des jeux.
    public static bool IsPauseTogglePressed() => Raylib.IsKeyPressed(Settings.KEY_ToggleGameMenu) || Raylib.IsKeyPressed(KeyboardKey.Escape) || BtnPressed(GamepadButton.MiddleRight); // Start / Menu
    public static bool IsMenuConfirmPressed() => Raylib.IsKeyPressed(KeyboardKey.Enter)           || BtnPressed(GamepadButton.RightFaceDown); // Entrée / A

    // ======================================================
    // NAVIGATION DANS LES MENUS (front montant = 1 déclenchement par appui)
    // Combine : les touches de DÉPLACEMENT que le joueur a configurées
    // (avancer/reculer = haut/bas, gauche/droite), les FLÈCHES, la CROIX
    // DIRECTIONNELLE et le STICK GAUCHE de la manette.
    // ======================================================
    static bool stickUpLatch, stickDownLatch, stickLeftLatch, stickRightLatch;

    // Front montant du stick dans une direction : vrai une seule fois quand on
    // le pousse au-delà du seuil (puis il faut relâcher pour redéclencher).
    static bool StickEdge(GamepadAxis axis, float sign, ref bool latch)
    {
        bool active = GamepadConnected() && Axis(axis) * sign > 0.5f;
        bool edge = active && !latch;
        latch = active;
        return edge;
    }

    public static bool IsMenuUpPressed() =>
        Raylib.IsKeyPressed(Settings.KEY_MoveForward)  || Raylib.IsKeyPressed(Settings.KEY_MoveForwardAlt)  ||
        Raylib.IsKeyPressed(KeyboardKey.Up)    || BtnPressed(GamepadButton.LeftFaceUp)    || StickEdge(GamepadAxis.LeftY, -1f, ref stickUpLatch);

    public static bool IsMenuDownPressed() =>
        Raylib.IsKeyPressed(Settings.KEY_MoveBackward) || Raylib.IsKeyPressed(Settings.KEY_MoveBackwardAlt) ||
        Raylib.IsKeyPressed(KeyboardKey.Down)  || BtnPressed(GamepadButton.LeftFaceDown)  || StickEdge(GamepadAxis.LeftY,  1f, ref stickDownLatch);

    public static bool IsMenuLeftPressed() =>
        Raylib.IsKeyPressed(Settings.KEY_MoveLeft)     || Raylib.IsKeyPressed(Settings.KEY_MoveLeftAlt)     ||
        Raylib.IsKeyPressed(KeyboardKey.Left)  || BtnPressed(GamepadButton.LeftFaceLeft)  || StickEdge(GamepadAxis.LeftX, -1f, ref stickLeftLatch);

    public static bool IsMenuRightPressed() =>
        Raylib.IsKeyPressed(Settings.KEY_MoveRight)    || Raylib.IsKeyPressed(Settings.KEY_MoveRightAlt)    ||
        Raylib.IsKeyPressed(KeyboardKey.Right) || BtnPressed(GamepadButton.LeftFaceRight) || StickEdge(GamepadAxis.LeftX,  1f, ref stickRightLatch);

    /// <summary>
    /// Rotation caméra demandée au stick droit, déjà passée par la zone morte et une
    /// courbe quadratique (précision au centre, rapidité au bord). X = horizontal,
    /// Y = vertical. L'appelant multiplie par la sensibilité et le deltaTime.
    /// </summary>
    public static Vector2 GetGamepadLook()
    {
        if (!GamepadConnected()) return Vector2.Zero;
        float rx = DeadzoneCurve(Raylib.GetGamepadAxisMovement(GamepadIndex, GamepadAxis.RightX));
        float ry = DeadzoneCurve(Raylib.GetGamepadAxisMovement(GamepadIndex, GamepadAxis.RightY));
        return new Vector2(rx, ry);
    }

    // Applique la zone morte puis une courbe quadratique à un axe de stick (-1..1).
    static float DeadzoneCurve(float v)
    {
        float m = MathF.Abs(v);
        if (m < StickDeadzone) return 0f;
        float n = (m - StickDeadzone) / (1f - StickDeadzone); // remis à l'échelle 0..1 après la zone morte
        n = n * n;                                            // courbe douce : lent au centre, rapide au bord
        return MathF.Sign(v) * n;
    }

    // ===== MOVEMENT KEYS (Direct access to Settings) =====
    public static KeyboardKey MoveForward => Settings.KEY_MoveForward;
    public static KeyboardKey MoveForwardAlt => Settings.KEY_MoveForwardAlt;
    public static KeyboardKey MoveBackward => Settings.KEY_MoveBackward;
    public static KeyboardKey MoveBackwardAlt => Settings.KEY_MoveBackwardAlt;
    public static KeyboardKey MoveLeft => Settings.KEY_MoveLeft;
    public static KeyboardKey MoveLeftAlt => Settings.KEY_MoveLeftAlt;
    public static KeyboardKey MoveRight => Settings.KEY_MoveRight;
    public static KeyboardKey MoveRightAlt => Settings.KEY_MoveRightAlt;

    // ===== ACTION KEYS =====
    public static KeyboardKey Jump => Settings.KEY_Jump;
    public static KeyboardKey Sprint => Settings.KEY_Sprint;
    public static KeyboardKey Crouch => Settings.KEY_Crouch;
    public static KeyboardKey Dash => Settings.KEY_Dash;
    public static KeyboardKey Reload => Settings.KEY_Reload;

    // ===== MENU KEYS =====
    public static KeyboardKey ToggleGameMenu => Settings.KEY_ToggleGameMenu;
    public static KeyboardKey SelectMenu => Settings.KEY_SelectMenu;

    // ===== DEBUG KEYS =====
    public static KeyboardKey DebugTeleportCenter => Settings.KEY_DebugTeleportCenter;
    public static KeyboardKey DebugTakeDamage => Settings.KEY_DebugTakeDamage;
    public static KeyboardKey DebugHeal => Settings.KEY_DebugHeal;
    public static KeyboardKey DebugPrintPosition => Settings.KEY_DebugPrintPosition;
    public static KeyboardKey DebugToggleInfo => Settings.KEY_DebugToggleInfo;
    public static KeyboardKey DebugToggleWeapon => Settings.KEY_DebugToggleWeapon;
}
