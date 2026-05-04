#version 330

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec3 fragNormal;
in vec4 fragColor;

uniform vec3 lightPos;      
uniform vec4 colDiffuse;    
uniform sampler2D texture0; 

out vec4 finalColor;

void main()
{
    // 1. Lumière Ambiante
    vec3 ambient = vec3(0.3, 0.3, 0.3); 

    // 2. Lumière Diffuse 
    vec3 norm = normalize(fragNormal);
    vec3 lightDir = normalize(lightPos - fragPosition);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * vec3(1.0, 1.0, 1.0); 

    // ========================================================
    // LA CORRECTION EST ICI
    // Change cette valeur pour ajuster la taille de la texture !
    // > 1.0 : La texture se répète plus (elle paraîtra plus petite)
    // < 1.0 : La texture s'étire (elle paraîtra plus grande)
    // ========================================================
    float textureScale = 100.0; // Essaie 2.0, 5.0, 10.0 ou 0.5 selon ton besoin
    vec2 scaledTexCoord = fragTexCoord * textureScale;

    // On utilise nos nouvelles coordonnées mises à l'échelle
    vec4 texelColor = texture(texture0, scaledTexCoord);

    // 3. Résultat final
    vec3 result = (ambient + diffuse) * texelColor.rgb * colDiffuse.rgb;
    finalColor = vec4(result, texelColor.a);
}