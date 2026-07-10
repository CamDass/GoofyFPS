using Raylib_cs;
using System.Numerics;

public class EffetClic
{
    public Vector2 Position;
    public Texture2D Texture;
    public int Opacite;

    // Le Constructeur : ce qui se passe quand le clic naît
    public EffetClic(Vector2 pos, Texture2D tex)
    {
        Position = pos;
        Texture = tex;
        Opacite = 255; // 255 = Totalement opaque (visible)
    }
}