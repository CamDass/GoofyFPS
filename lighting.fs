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

    // AMÉLIORATION : on passe les couleurs en espace LINÉAIRE.
    // Les calculs de lumière (addition/mélange) ne sont corrects qu'en linéaire.
    // On repasse en gamma tout à la fin -> tes couleurs gardent EXACTEMENT le même rendu,
    // mais les transitions lumière/ombre deviennent naturelles au lieu de "fades".
    vec3 baseLin  = pow(baseColor.rgb, vec3(2.2));
    vec3 lightLin = pow(lightColor.rgb, vec3(2.2));

    // AMÉLIORATION : on renormalise la normale.
    // Entre deux sommets elle est interpolée et perd sa longueur de 1 -> l'éclairage bave.
    vec3 N = normalize(fragNormal);
    vec3 viewDir = normalize(viewPos - fragPosition);

    // 2. LUMIÈRE
    // AMÉLIORATION : ambient HÉMISPHÉRIQUE (gris) — remplit les faces à l'ombre pour qu'elles
    // ne soient plus quasi-noires (sinon une face avant sombre ressemble à un "trou" et le bloc
    // paraît creux). Gris pur => ne change pas ta palette ; juste plus clair, le haut un peu plus
    // éclairé que le bas -> les blocs se lisent comme des volumes pleins, plus proche d'Eevee.
    float hemi = N.y * 0.5 + 0.5;                 // 0 = face vers le bas, 1 = face vers le haut
    float ambStrength = mix(0.42, 0.62, hemi);    // avant : 0.30 plat -> nettement plus lumineux
    vec3 ambient = pow(vec3(ambStrength), vec3(2.2));
    vec3 lightDir = normalize(lightPos - fragPosition);
    float diff = max(dot(N, lightDir), 0.0);
    vec3 diffuse = diff * lightLin;

    // AMÉLIORATION : SPÉCULAIRE (Blinn-Phong) -> reflets et volume sur les surfaces.
    // Utilise ta couleur de soleil existante, donc aucune nouvelle couleur introduite.
    vec3 halfDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(N, halfDir), 0.0), 48.0); // reflet plus resserré (moins d'effet "plastique")
    vec3 specular = spec * lightLin * 0.08;            // intensité fortement réduite : surfaces mates (bois/béton non brillants)
    specular *= diff;                                  // pas de reflet sur une face dans l'ombre

    // L'objet éclairé (avant le brouillard), en espace linéaire
    vec3 lightingResult = (ambient + diffuse) * baseLin + specular;

    // ==========================================
    // 3. LE BROUILLARD (FOG)
    // ==========================================
    vec3 finalRGB = lightingResult;
    if (applyFog)
    {
        // Calcul de la distance entre tes yeux et le mur
        float dist = length(viewPos - fragPosition);
        
        // Réglages du brouillard (Tu pourras modifier ces chiffres !)
        float fogStart = 40.0; // Le brouillard commence à 25 mètres
        float fogEnd = 150.0;   // On ne voit plus rien à 100 mètres
        
        // On calcule un pourcentage d'opacité du brouillard (entre 0.0 et 1.0)
        float fogFactor = clamp((dist - fogStart) / (fogEnd - fogStart), 0.0, 1.0);
        
        // Couleur du brouillard (C'est exactement le orange de ton Horizon de l'Étape 4 !)
        // Linéarisée pour rester dans le même espace que le reste du calcul (même rendu à l'écran).
        vec3 fogColor = pow(vec3(120.0/255.0, 60.0/255.0, 50.0/255.0), vec3(2.2));

        // On utilise "mix" pour mélanger la vraie couleur avec le brouillard selon la distance
        finalRGB = mix(lightingResult, fogColor, fogFactor);
    }

    // AMÉLIORATION : retour en espace gamma (sinon l'image entière paraît trop sombre).
    // C'est ce qui rapproche le plus le rendu de Blender (qui travaille aussi en linéaire).
    finalRGB = pow(finalRGB, vec3(1.0/2.2));

    // Résultat final !
    finalColor = vec4(finalRGB, baseColor.a);
}