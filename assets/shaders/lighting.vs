#version 330

// Ce que Raylib nous envoie par défaut
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

// Les matrices mathématiques de la caméra
uniform mat4 mvp;
uniform mat4 matModel;

// Ce qu'on va envoyer au Fragment Shader
out vec3 fragPosition;
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;

void main()
{
    // On calcule la position 3D réelle de ce point
    fragPosition = vec3(matModel * vec4(vertexPosition, 1.0));
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    
    // On garde l'orientation de la face (la Normale)
    mat3 normalMatrix = transpose(inverse(mat3(matModel)));
    fragNormal = normalize(normalMatrix * vertexNormal);
    
    // On place le point sur l'écran
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}