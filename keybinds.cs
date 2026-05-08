using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;
using System.Linq;

/// <summary>
/// Keyboard input helper methods that reference Settings
/// This allows for runtime keybind customization
/// </summary>
public static class KeyBinds
{
    // All keybinds are now accessed through Settings class for easy customization

    // ===== HELPER METHODS =====
    /// <summary>
    /// Check if movement is requested (any direction key)
    /// </summary>
    public static bool IsMoveForwardPressed() => Raylib.IsKeyDown(Settings.KEY_MoveForward) || Raylib.IsKeyDown(Settings.KEY_MoveForwardAlt);
    public static bool IsMoveBackwardPressed() => Raylib.IsKeyDown(Settings.KEY_MoveBackward) || Raylib.IsKeyDown(Settings.KEY_MoveBackwardAlt);
    public static bool IsMoveLeftPressed() => Raylib.IsKeyDown(Settings.KEY_MoveLeft) || Raylib.IsKeyDown(Settings.KEY_MoveLeftAlt);
    public static bool IsMoveRightPressed() => Raylib.IsKeyDown(Settings.KEY_MoveRight) || Raylib.IsKeyDown(Settings.KEY_MoveRightAlt);

    public static bool IsSprintingPressed() => Raylib.IsKeyDown(Settings.KEY_Sprint);
    public static bool IsCrouchingPressed() => Raylib.IsKeyDown(Settings.KEY_Crouch);
    public static bool IsJumpingPressed() => Raylib.IsKeyPressed(Settings.KEY_Jump);
    public static bool IsDashingPressed() => Raylib.IsKeyPressed(Settings.KEY_Dash);
    public static bool IsReloadingPressed() => Raylib.IsKeyPressed(Settings.KEY_Reload);

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
    public static KeyboardKey ExitToMenu => Settings.KEY_ExitToMenu;
    public static KeyboardKey SelectMenu => Settings.KEY_SelectMenu;

    // ===== DEBUG KEYS =====
    public static KeyboardKey DebugTeleportCenter => Settings.KEY_DebugTeleportCenter;
    public static KeyboardKey DebugTakeDamage => Settings.KEY_DebugTakeDamage;
    public static KeyboardKey DebugHeal => Settings.KEY_DebugHeal;
    public static KeyboardKey DebugPrintPosition => Settings.KEY_DebugPrintPosition;
    public static KeyboardKey DebugToggleInfo => Settings.KEY_DebugToggleInfo;
    public static KeyboardKey DebugToggleWeapon => Settings.KEY_DebugToggleWeapon;
}


