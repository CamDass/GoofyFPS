#version 330

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform vec3 lightPos;
uniform vec4 lightColor;

uniform vec3 viewPos; 
uniform bool applyFog;

out vec4 finalColor;

void main()
{
    // 1. CORRECTION DES TEXTURES
    vec4 texelColor = texture(texture0, fragTexCoord);
    
    // On combine la texture avec la couleur du modèle (colDiffuse) et la couleur du sommet (fragColor)
    // Ça répare les objets sans texture !
    vec4 baseColor = texelColor * colDiffuse * fragColor;
    
    // Si le pixel est transparent, on l'ignore (utile pour les grillages ou feuilles)
    if (baseColor.a == 0.0) discard;

    // 2. LUMIÈRE
    vec3 ambient = vec3(0.3, 0.3, 0.3); // J'ai un peu éclairci les ombres pour plus de visibilité
    vec3 lightDir = normalize(lightPos - fragPosition);
    float diff = max(dot(fragNormal, lightDir), 0.0);
    vec3 diffuse = diff * lightColor.rgb;

    // L'objet éclairé (avant le brouillard)
    vec3 lightingResult = (ambient + diffuse) * baseColor.rgb;

    // ==========================================
    // 3. LE BROUILLARD (FOG)
    // ==========================================
    vec3 finalRGB = lightingResult;
    if (applyFog)
    {
        // Calcul de la distance entre tes yeux et le mur
        float dist = length(viewPos - fragPosition);
        
        // Réglages du brouillard (Tu pourras modifier ces chiffres !)
        float fogStart = 100.0; // Le brouillard commence à 25 mètres
        float fogEnd = 250.0;   // On ne voit plus rien à 100 mètres
        
        // On calcule un pourcentage d'opacité du brouillard (entre 0.0 et 1.0)
        float fogFactor = clamp((dist - fogStart) / (fogEnd - fogStart), 0.0, 1.0);
        
        // Couleur du brouillard (C'est exactement le orange de ton Horizon de l'Étape 4 !)
        vec3 fogColor = vec3(120.0/255.0, 60.0/255.0, 50.0/255.0);

        // On utilise "mix" pour mélanger la vraie couleur avec le brouillard selon la distance
        finalRGB = mix(lightingResult, fogColor, fogFactor);
    }

    // Résultat final !
    finalColor = vec4(finalRGB, baseColor.a);
}