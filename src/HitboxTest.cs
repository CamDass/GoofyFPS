using System;
using System.Numerics;
using Raylib_cs;

// ========================================================
// TEST VISUEL AUTOMATIQUE :  dotnet run -- --hitboxtest
// ========================================================
// Dessine un personnage par chapeau avec, par-dessus :
//   - en VERT  : la hitbox de tir réelle (elle épouse la capsule physique, Y-1 a Y+1)
//   - en ROUGE : l'ancienne rallonge de 0.5 m qui montait a Y+1.5
// Le chapeau doit tomber ENTIEREMENT dans la zone rouge : c'est la preuve qu'il
// n'est plus touchable. Les cosmetiques ne bloquent ni n'encaissent aucun tir.
// ========================================================
partial class Program
{
    public static void LancerHitboxTest()
    {
        Raylib.InitWindow(1760, 900, "GoofyFPS - HitboxTest");
        Raylib.SetTargetFPS(60);
        ChargerCosmetiques();

        RenderTexture2D rt = Raylib.LoadRenderTexture(1760, 900);

        Camera3D cam = new Camera3D
        {
            Position = new Vector3(0f, 2.6f, 9.2f),
            Target = new Vector3(0f, 0.9f, 0f),
            Up = new Vector3(0, 1, 0),
            FovY = 45f,
            Projection = CameraProjection.Perspective
        };

        int nb = Math.Max(chapeauxSkin.Count, 1);
        float esp = 2.1f;
        float x0 = -esp * (nb - 1) / 2f;

        Raylib.BeginTextureMode(rt);
        Raylib.ClearBackground(new Color(150, 195, 235, 255));
        Raylib.BeginMode3D(cam);
        Raylib.DrawPlane(new Vector3(0, -1f, 0), new Vector2(60, 60), new Color(110, 130, 110, 255));

        for (int i = 0; i < nb; i++)
        {
            Vector3 centre = new Vector3(x0 + i * esp, 0f, 0f);
            DessinerPersonnageComplet(centre, 0.35f, 0f, i % couleursSkin.Length, i, -1);

            // VERT : la hitbox de tir d'aujourd'hui = la capsule physique exactement.
            // (Weapon.Shoot et Network.TraiterTirLocalReseau utilisent centre +/- (0.5, 1, 0.5))
            Raylib.DrawCubeWires(centre, 1f, 2f, 1f, Color.Green);

            // ROUGE : les 0.5 m qu'on vient d'enlever. Le chapeau vit LA-DEDANS :
            // avant, lui tirer dessus infligeait les degats de l'arme.
            Raylib.DrawCubeWires(centre + new Vector3(0, 1.25f, 0), 1f, 0.5f, 1f, Color.Red);
        }

        Raylib.EndMode3D();

        for (int i = 0; i < nb; i++)
        {
            Vector2 e = Raylib.GetWorldToScreenEx(new Vector3(x0 + i * esp, 2.9f, 0f), cam, 1760, 900);
            string nom = i < chapeauxSkin.Count ? chapeauxSkin[i].Nom : "(aucun)";
            Raylib.DrawText(nom, (int)e.X - Raylib.MeasureText(nom, 18) / 2, (int)e.Y, 18, Color.Black);
        }

        Raylib.DrawText("HITBOX DE TIR", 20, 20, 28, Color.Black);
        Raylib.DrawText("VERT  = hitbox reelle (capsule physique, Y-1 a Y+1)", 20, 58, 20, new Color(0, 110, 0, 255));
        Raylib.DrawText("ROUGE = rallonge supprimee (Y+1 a Y+1.5) : la zone du chapeau", 20, 84, 20, new Color(160, 0, 0, 255));
        Raylib.DrawText("Le chapeau doit etre ENTIEREMENT dans le rouge = plus aucune hitbox.", 20, 110, 20, Color.DarkGray);

        Raylib.EndTextureMode();

        Image img = Raylib.LoadImageFromTexture(rt.Texture);
        Raylib.ImageFlipVertical(ref img);
        Raylib.ExportImage(img, "hitboxtest.png");
        Raylib.UnloadImage(img);

        Raylib.UnloadRenderTexture(rt);
        Raylib.CloseWindow();
        Console.WriteLine($"[HITBOXTEST] {chapeauxSkin.Count} chapeaux -> hitboxtest.png");
    }
}
