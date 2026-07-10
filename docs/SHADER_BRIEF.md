# GoofyFPS — Brief technique pour le dev shaders

But du document : donner tout le contexte pour **re-développer le système de shaders** afin qu'il s'adapte proprement aux maps, aux armes et aux textures. Le shader actuel est un forward unique et générique appliqué à tout — c'est justement ce qu'on veut faire évoluer.

---

## 1. Stack & pipeline de rendu

- **Moteur** : Raylib-cs 7.0 (OpenGL 3.3, GLSL `#version 330`). C# / .NET 10.
- **Physique** : BepuPhysics v2, **totalement séparée du rendu** (mesh de collision reconstruit depuis les triangles — voir §7). Les shaders n'ont aucun impact sur la physique.
- **Un seul shader** partagé par tout ce qui est 3D : `lighting.vs` + `lighting.fs`. Il est assigné à la map, aux armes, aux ennemis et aux barils au chargement (`Program.cs`, boucles `Materials[i].Shader = lightShader`).
- **Deux passes 3D par frame** (dans `Jeu.cs`, `BeginMode3D`) :
  1. **Monde** : `camera` (FOV joueur), `applyFog = 1`, lumière = soleil monde.
  2. **Vue-arme (viewmodel)** : `weaponCamera` séparée, `applyFog = 0`, lumière repositionnée près de l'arme (`soleilVueArme`). Rendu par-dessus, depth buffer nettoyé.
- **Ciel** : pas de skybox 3D — un simple `DrawRectangleGradientV` 2D avant la passe 3D. Zénith `RGB(20,25,45)` (bleu nuit) → horizon `RGB(120,60,50)` (orange sale). Ambiance crépuscule.
- **HUD** : 2D pur, dessiné après les passes 3D (police pixel, barres de vie, munitions). Hors scope shader 3D.
- **Post-process** : aucun (pas de bloom, SSAO, tonemapping séparé, TAA…). Tout est fait dans le fragment shader.

---

## 2. Le shader actuel (à remplacer)

### `lighting.vs`
Standard : sort `fragPosition` (world space via `matModel`), `fragTexCoord`, `fragColor` (couleur de sommet), `fragNormal` (normal matrix = `transpose(inverse(mat3(matModel)))`). `gl_Position = mvp * pos`.

### `lighting.fs` — modèle d'éclairage
```
baseColor = texture(texture0, uv) * colDiffuse * fragColor;   // discard si alpha==0
// éclairage en espace LINÉAIRE (pow 2.2 en entrée, pow 1/2.2 en sortie)
ambient  = 0.3
diffuse  = max(dot(N, L), 0) * lightColor           // 1 seule lumière ponctuelle
specular = pow(max(dot(N,H),0), 48) * lightColor * 0.08 * diff   // Blinn-Phong, réduit
result   = (ambient + diffuse) * baseColor + specular
// FOG optionnel : mix vers RGB(120,60,50) entre 40 m et 150 m (linéaire)
```

### Limites connues (à corriger dans la refonte)
- **Spéculaire uniforme** : même reflet sur tout → bois, béton et métal réagissent pareil (pas physiquement cohérent). Actuellement baissé à `0.08` en compromis « tout mat ».
- **Aucune lecture de `metallic` / `roughness` par matériau**, alors que les glb les portent (voir §5/§6). Le shader ignore ces facteurs.
- **Aucune normal map** échantillonnée (les normales viennent uniquement de la géométrie), alors que plusieurs assets embarquent des normal maps.
- **Une seule lumière**, pas d'ombres, pas d'IBL/ambient directionnel.
- **Fog en dur** (couleur + distances) — pas adapté par map (problématique sur la grande map, voir §4).

### Uniforms attendus (locations récupérées dans `Program.cs`)
| Uniform | Source (code) | Valeur / note |
|---|---|---|
| `mvp`, `matModel` | auto Raylib | — |
| `texture0` | auto (map ALBEDO) | texture diffuse ; blanc 1×1 par défaut si non texturé |
| `colDiffuse` | auto (baseColorFactor × 255) | teinte par matériau |
| `fragColor` | vertex color | blanc par défaut si absent |
| `lightPos` | `soleilPosition = (50,100,50)` monde | repositionné près caméra en passe arme |
| `lightColor` | `(1.0, 0.9, 0.8, 1)` | soleil chaud |
| `viewPos` | `camera.Position` | pour spéculaire + fog |
| `applyFog` | `1` monde / `0` arme | int bool |

> Si tu ajoutes des uniforms (roughness, metallic, normalMap, lightDir directionnelle…), pense à les récupérer via `GetShaderLocation` dans `Program.cs` (~L1025) et à les set dans `Jeu.cs` (~L1230 monde, ~L1615 arme). Les textures secondaires par matériau doivent être bindées par slot (`MaterialMapIndex.Normal`, `.Metalness`, `.Roughness`) — Raylib expose déjà ces slots.

---

## 3. Vue d'ensemble des 4 maps

| # | Fichier | Nom en jeu | Meshes | Taille approx (X,Y,Z) m | Matériaux | Textures | Style |
|---|---------|-----------|--------|--------------------------|-----------|----------|-------|
| 0 | `test.glb` | Tutoriel | 15 | 91 × 68 × 97 | 9 | ❌ couleurs unies | Bloc-out simple |
| 1 | `map.glb` | La Ville | 98 | 63 × 102 × 94 | 48 (47 texturés) | ✅ 7 images (13 Mo) | Ville verticale texturée |
| 2 | `sandbox.glb` | Chantier | **858** | **2245 × 165 × 1097** | 12 | ❌ couleurs unies | Parkour/sandbox géant |
| 3 | `blockmap.glb` | Blocs | 112 | 146 × 40 × 86 | 20 (tous texturés) | ✅ 7 images 1K (1.3 Mo) | Port/chantier sur pilier |

Convention monde : **Y = haut** (Raylib). Sol des maps ≈ `Y = 0`. `mapScale = 1`, `mapPosition = (0,0,0)`.

---

## 4. Description par map (pour adapter le shader)

### Map 0 — Tutoriel (`test.glb`)
- Petit espace de démarrage, 15 meshes, **couleurs unies** (pas de texture, `colDiffuse` uniquement).
- Peu d'enjeu visuel. Un shader qui gère bien les surfaces non texturées (texture blanche par défaut × couleur) suffit. Sert de cas « flat color » à ne pas casser.

### Map 1 — La Ville (`map.glb`)
- **La plus texturée** : 47/48 matériaux avec baseColorTexture, 7 images, 13 Mo. Ville **verticale** (Y jusqu'à ~102 m) : toits, façades, ruelles.
- Enjeux shader : lisibilité des façades texturées à moyenne distance, éclairage cohérent sur surfaces verticales, occlusion/contraste (les ruelles manquent de profondeur avec 1 seule lumière + ambient plat). Candidate n°1 pour **normal maps + AO**.
- Vérifier le tiling/échelle des UV (map importée telle quelle, non retravaillée par nos soins).

### Map 2 — Chantier (`sandbox.glb`)
- **Énorme** : 858 meshes, étendue **~2.2 km × 1.1 km**. Couleurs unies (12 matériaux). Terrain de parkour : rampes, plateformes, **jump pads** (logique gameplay dans `Jeu.cs`, cherche « Jump pad »).
- Enjeux shader spécifiques :
  - **Le fog en dur (40→150 m) est inadapté** : sur une map de 2 km, tout est noyé dans le brouillard. Il faut un **fog paramétrable par map** (distances, couleur, on/off).
  - Gérer la **profondeur/échelle** (précision depth, pas de z-fighting sur grandes distances).
  - Style « bloc-out coloré » assumé : un shader qui rend bien les couleurs plates avec un bon shading directionnel (pas besoin de PBR ici).

### Map 3 — Blocs (`blockmap.glb`) — la plus travaillée récemment
- Arène symétrique type **port/chantier** : conteneurs, caisses bois, cabanes, murs, posée sur une **dalle + gros pilier** qui descend à `Y = -30`. Sol à `Y = 0`.
- **100 % texturée PBR (diffuse)** : 20 matériaux, 7 images PolyHaven en **1K** (béton, bois ×2, tôle ondulée, tôle, métal rouillé). Chaque matériau = `baseColorTexture × baseColorFactor`.
  - Les **conteneurs** = une texture métal (ondulée/rouillée/plaque) **teintée** via `baseColorFactor` en ~11 couleurs → un shader doit respecter `texel × colDiffuse`.
- **UV** : box-projection **alignée monde** cuite dans le mesh (tuiles 3–8 m selon catégorie). ⚠️ **le tiling est dans les UV** (valeurs > 1), pas dans un node Mapping — car le jeu **ne supporte pas `KHR_texture_transform`**. Toute refonte qui voudrait re-scaler les textures doit le faire soit dans les UV, soit ajouter le support de la transform.
- **Mipmaps** : générés côté code (`Program.cs ApplyMapTextures()` → `GenTextureMipmaps` + `SetTextureFilter(Trilinear)`), sinon fort aliasing/scintillement des textures tuilées au loin. À conserver ou remplacer par un sampling correct.
- Catégorie de chaque bloc disponible dans le `.blend` (`obj["mapcat"]` : ground/pillar/platform/cabin/container/crate/wall) — utile si tu veux un shader qui différencie les matériaux.
- Enjeu principal demandé : **différencier métal vs bois vs béton** (réflectivité). Idéal = lire `metallic`/`roughness` par matériau (le `.blend` a la roughness ; on peut ré-exporter les facteurs PBR dans le glb sur demande).

---

## 5. Armes, ennemis, props

- **Armes** (`assets/3D/`) : `sniper, karambit, bazooka, sword, shotgun, pistol, revolver` — modèles **low-poly texturés PBR** (3 à 6 images chacun, incluent probablement normal/roughness que le shader actuel **n'utilise pas**). Rendues en **passe viewmodel** (caméra dédiée, fog off, lumière rapprochée).
  - Besoin : un shader viewmodel qui met en valeur le métal des armes (spéculaire/metallic **par matériau**) sans être « plastique ». C'est le contraste avec les surfaces mates des maps.
- **Ennemis** (`ennemy.glb`) : **non texturé** (blanc plat, 1 matériau). Barres de vie 3D au-dessus.
- **Barils** (`barril.glb`) : texturé (3 images). Explosent (effets 2D/sphères).

---

## 6. Ce que doit apporter la refonte (résumé priorisé)

1. **PBR par matériau** : lire `metallic` + `roughness` (glb/Raylib slots `MaterialMapIndex.Metalness/.Roughness`) → métal réfléchissant, bois/béton mats. C'est LA demande principale.
2. **Normal mapping** (slot `.Normal`) pour les maps texturées (Ville, Blocs) et les armes → casser l'effet « feuille de papier plate ».
3. **Fog paramétrable par map** (distances/couleur/on-off) — critique pour le Chantier (2 km) vs Blocs (150 m).
4. **Éclairage amélioré** : au moins une lumière directionnelle propre + meilleur ambient (hémisphérique/IBL simple) ; ombres optionnelles.
5. **Garder** : workflow gamma linéaire, discard alpha, mipmaps/trilinéaire, la passe viewmodel (fog off), compat couleurs unies (maps 0/2) ET texturées (maps 1/3).

---

## 7. Gotchas techniques

- **Espace** : Y-up. `fragPosition`/`viewPos`/`lightPos` en **world space**.
- **Winding & collisions** : la physique (`Program.cs ExtraireTrianglesMap`) reconstruit le mesh Bepu depuis l'**ordre des sommets** (winding) des triangles — indépendant du rendu, mais **ne pas inverser les normales/winding à l'export** sous peine de collisions à sens unique. Les normales des maps sont garanties « outward ».
- **Texture par défaut** : les matériaux sans texture reçoivent un blanc 1×1 → `texture0` renvoie blanc, le résultat vient de `colDiffuse`. Le shader doit rester correct dans ce cas (maps 0 et 2).
- **Wrap** : REPEAT (UV > 1 pour le tiling). Pas de `KHR_texture_transform` supporté aujourd'hui.
- **Vertex colors** : `fragColor` = blanc par défaut si le mesh n'a pas de COLOR_0. Le shader multiplie par lui (`texel × colDiffuse × fragColor`).
- **Fichiers** : shaders dans `lighting.vs` / `lighting.fs` (racine) ; locations dans `Program.cs` (~L1025) ; envoi des uniforms dans `Jeu.cs` (monde ~L1230, arme ~L1615).
