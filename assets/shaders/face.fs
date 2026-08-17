#version 330

// Shader des VISAGES de skin : identique au shader par défaut de raylib,
// avec un seul ajout : les pixels transparents sont JETÉS (discard) au lieu
// d'être dessinés invisibles. Sans ça, le contour transparent du visage
// écrirait quand même dans le tampon de profondeur et pourrait masquer un
// joueur, un mur ou un tir dessiné juste après.

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

out vec4 finalColor;

void main()
{
    // Seuil bas : avec les mipmaps, les traits fins d'un dessin voient leur
    // alpha moyenné vers le bas quand on s'éloigne ; un seuil trop haut les
    // transformerait en pointillés. Le biais -1.0 garde un mip plus net
    // (les visages sont de petits dessins au trait, pas des photos).
    vec4 texelColor = texture(texture0, fragTexCoord, -1.0);
    if (texelColor.a < 0.12) discard;
    finalColor = texelColor * colDiffuse * fragColor;
}
