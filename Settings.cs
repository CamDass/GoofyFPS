using System;
using System.Collections.Generic;
using Raylib_cs;

/// <summary>
/// Centralized game settings management
/// Handles volumes, FOV, and all customizable keybinds
/// </summary>
public static class Settings
{
    // ===== AUDIO SETTINGS =====
    public static float MasterVolume = 0.8f;  // 0.0 - 1.0
    public static float SFXVolume = 0.8f;     // 0.0 - 1.0
    public static float MusicVolume = 0.7f;   // 0.0 - 1.0

    // ===== GAMEPLAY SETTINGS =====
    public static float BaseFOV = 60f;        // Base field of view
    public static float MouseSensitivity = 0.003f;

    // ===== CUSTOMIZABLE KEYBINDS (Mutable) =====
    // Movement
    public static KeyboardKey KEY_MoveForward = KeyboardKey.W;
    public static KeyboardKey KEY_MoveForwardAlt = KeyboardKey.Up;
    public static KeyboardKey KEY_MoveBackward = KeyboardKey.S;
    public static KeyboardKey KEY_MoveBackwardAlt = KeyboardKey.Down;
    public static KeyboardKey KEY_MoveLeft = KeyboardKey.A;
    public static KeyboardKey KEY_MoveLeftAlt = KeyboardKey.Left;
    public static KeyboardKey KEY_MoveRight = KeyboardKey.D;
    public static KeyboardKey KEY_MoveRightAlt = KeyboardKey.Right;

    // Actions
    public static KeyboardKey KEY_Jump = KeyboardKey.Space;
    public static KeyboardKey KEY_Sprint = KeyboardKey.LeftShift;
    public static KeyboardKey KEY_Crouch = KeyboardKey.C;
    public static KeyboardKey KEY_Dash = KeyboardKey.LeftControl;
    public static KeyboardKey KEY_Reload = KeyboardKey.R;
    public static KeyboardKey KEY_BuildWall = KeyboardKey.F;

    // Menu
    public static KeyboardKey KEY_ToggleGameMenu = KeyboardKey.Tab;
    public static KeyboardKey KEY_ExitToMenu = KeyboardKey.Tab;
    public static KeyboardKey KEY_SelectMenu = KeyboardKey.Space;
    

    // Debug
    public static KeyboardKey KEY_DebugTeleportCenter = KeyboardKey.P;
    public static KeyboardKey KEY_DebugTakeDamage = KeyboardKey.K;
    public static KeyboardKey KEY_DebugHeal = KeyboardKey.H;
    public static KeyboardKey KEY_DebugPrintPosition = KeyboardKey.RightShift;
    public static KeyboardKey KEY_DebugToggleInfo = KeyboardKey.F2;
    public static KeyboardKey KEY_DebugToggleWeapon = KeyboardKey.F3;

    /// <summary>
    /// Get the name of a keyboard key for display
    /// </summary>
    public static string GetKeyName(KeyboardKey key)
    {
        return key switch
        {
            KeyboardKey.W => "W",
            KeyboardKey.A => "A",
            KeyboardKey.S => "S",
            KeyboardKey.D => "D",
            KeyboardKey.Up => "↑",
            KeyboardKey.Down => "↓",
            KeyboardKey.Left => "←",
            KeyboardKey.Right => "→",
            KeyboardKey.Space => "ESPACE",
            KeyboardKey.LeftShift => "MAJ",
            KeyboardKey.LeftControl => "CTRL",
            KeyboardKey.LeftAlt => "ALT",
            KeyboardKey.Tab => "TAB",
            KeyboardKey.R => "R",
            KeyboardKey.C => "C",
            KeyboardKey.P => "P",
            KeyboardKey.K => "K",
            KeyboardKey.H => "H",
            KeyboardKey.RightShift => "SHIFT D",
            KeyboardKey.F2 => "F2",
            KeyboardKey.F3 => "F3",
            _ => key.ToString()
        };
    }

    /// <summary>
    /// Apply volume settings to all sounds
    /// </summary>
    public static void ApplySoundVolumes()
    {
        // This will be called after loading sounds in Program.cs
        // Raylib will adjust the master volume through SetMasterVolume() if available
    }

    /// <summary>
    /// Apply FOV setting to camera
    /// </summary>
    public static void ApplyFOVSettings(ref Camera3D camera)
    {
        camera.FovY = BaseFOV;
    }
}
