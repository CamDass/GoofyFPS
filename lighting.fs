#version 330
in vec3 fragPosition;
in vec2 fragTexCoord;
in vec3 fragNormal;
in vec4 fragColor;

uniform vec3 lightPos; // La position de notre lumière
uniform vec4 colDiffuse; // La couleur de base de l'objet

out vec4 finalColor;

void main()
{
    // 1. Lumière Ambiante (pour que les zones à l'ombre ne soient pas 100% noires)
    vec3 ambient = vec3(0.2, 0.2, 0.2);

    // 2. Lumière Diffuse (la lumière qui tape directement l'objet)
    vec3 norm = normalize(fragNormal);
    vec3 lightDir = normalize(lightPos - fragPosition);
    
    // On calcule l'angle entre la face du mur et la lumière
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * vec3(1.0, 1.0, 1.0); // Lumière blanche

    // Résultat final
    vec3 result = (ambient + diffuse) * vec3(colDiffuse);
    finalColor = vec4(result, 1.0);
}