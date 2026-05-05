# GoofyFPS

Un jeu FPS (First-Person Shooter) développé en C# utilisant Raylib pour le rendu graphique et BepuPhysics pour la simulation physique. Le jeu propose une expérience de survie avec des combats contre des ennemis, plusieurs armes, et une physique réaliste.

## Table des Matières

- [Aperçu](#aperçu)
- [Fonctionnalités](#fonctionnalités)
- [Architecture](#architecture)
- [Technologies Utilisées](#technologies-utilisées)
- [Installation et Lancement](#installation-et-lancement)
- [Structure du Projet](#structure-du-projet)
- [Classes Principales](#classes-principales)
- [Systèmes](#systèmes)
- [Assets](#assets)
- [Contrôles](#contrôles)
- [Développement](#développement)
- [Licence](#licence)

## Aperçu

GoofyFPS est un jeu de tir à la première personne où le joueur doit survivre à des vagues d'ennemis dans différents environnements. Le jeu intègre :

- Physique réaliste avec BepuPhysics
- Système d'éclairage dynamique avec shaders
- Système audio complet avec musique et effets sonores
- Interface utilisateur avec menus et HUD
- Plusieurs armes avec mécaniques différentes
- Système de santé et dégâts

## Fonctionnalités

### Gameplay
- **Mode Survie** : Combattez des vagues d'ennemis qui apparaissent périodiquement
- **Système de Santé** : Gérez votre santé avec possibilité de soin
- **Armes Diversifiées** : Sniper, fusil à pompe, pistolet, revolver, bazooka, épée, karambit
- **Physique Réaliste** : Mouvements, sauts, dash, et interactions physiques
- **Système de Recul** : Chaque arme a un recul physique affectant le mouvement du joueur

### Graphismes
- **Éclairage Dynamique** : Shader personnalisé avec lumière directionnelle et brouillard
- **Modèles 3D** : Environnements et armes en 3D
- **Effets Visuels** : Particules de clic, explosions, textes de dégâts flottants
- **Interface** : Menus avec animations et effets visuels

### Audio
- **Musique Dynamique** : Musique différente pour le menu et le jeu
- **Effets Sonores** : Tirs, impacts, sons d'environnement
- **Gestion Audio** : Volume réglable, pool de sons pour éviter les conflits

## Architecture

Le projet suit une architecture orientée objet avec séparation des responsabilités :

```
GoofyFPS/
├── Program.cs          # Point d'entrée principal, gestion globale
├── Menu.cs             # Interface menu principal et sélection carte
├── Jeu.cs              # Boucle principale du jeu
├── Menugame.cs         # Menu pause en jeu
├── player.cs           # Classe du joueur
├── ennemi.cs           # Classe des ennemis
├── Weapon.cs           # Classes des armes
├── Physics.cs          # Callbacks et capteurs physiques
├── EffetClic.cs        # Système de particules de clic
├── lighting.vs/fs      # Shaders pour l'éclairage
└── assets/             # Ressources graphiques et audio
```

### Flux d'Exécution

1. **Initialisation** (`Main()`)
   - Chargement des assets (textures, modèles, sons)
   - Configuration de la physique BepuPhysics
   - Configuration des shaders et éclairage

2. **Boucle Principale**
   - Menu principal → Sélection de carte → Jeu
   - Gestion des états : menu, choix carte, jeu

3. **Nettoyage**
   - Libération de toutes les ressources (textures, modèles, sons)

## Technologies Utilisées

- **Langage** : C# 11.0
- **Framework Graphique** : Raylib-cs 7.0.2
- **Moteur Physique** : BepuPhysics 2.4.0
- **Shaders** : GLSL (OpenGL 3.3)
- **Plateforme** : Windows (compilé pour .NET 11.0)

## Installation et Lancement

### Prérequis

- .NET 11.0 SDK
- Windows 10/11
- Carte graphique compatible OpenGL 3.3+

### Installation

1. Clonez le repository :
```bash
git clone https://github.com/camil/GoofyFPS.git
cd GoofyFPS
```

2. Restaurez les dépendances :
```bash
dotnet restore
```

3. Lancez le jeu :
```bash
dotnet run
```

### Compilation

Pour compiler en mode Release :
```bash
dotnet build --configuration Release
```

Le binaire se trouve dans `bin/Release/net11.0/GoofyFPS.exe`

## Structure du Projet

### Fichiers Source

- **Program.cs** : Classe principale contenant toutes les variables globales, l'initialisation et la gestion des états
- **Menu.cs** : Gestion de l'interface menu et sélection de carte
- **Jeu.cs** : Logique principale du jeu, rendu 3D, physique, ennemis
- **Menugame.cs** : Menu pause accessible en jeu
- **player.cs** : Classe Player avec gestion de la santé
- **ennemi.cs** : Classe Enemy avec IA et comportement
- **Weapon.cs** : Classes d'armes avec mécaniques de tir
- **Physics.cs** : Callbacks BepuPhysics et capteurs
- **EffetClic.cs** : Système de particules pour les clics souris

### Shaders

- **lighting.vs** : Vertex shader pour l'éclairage
- **lighting.fs** : Fragment shader avec éclairage et brouillard

### Assets

```
assets/
├── 2D/                 # Textures 2D (boutons, HUD, effets)
├── 3D/                 # Modèles 3D (armes, ennemis, environnement)
└── sounds/             # Audio (musique, effets sonores)
```

## Classes Principales

### Player

```csharp
public class Player
{
    public int MaxHealth;    // Santé maximale
    public int Health;       // Santé actuelle
    public bool IsAlive;     // État de vie

    public void TakeDamage(int amount);  // Réception de dégâts
    public void Heal(int amount);        // Soin
    public void Respawn();               // Réapparition
}
```

**Responsabilités** :
- Gestion de la santé et de la mort
- Système de soin et respawn
- Effets sonores de mort

### Enemy

```csharp
public class Enemy
{
    public BodyHandle bodyId;    // Référence physique
    public int health;           // Santé
    public float speed;          // Vitesse de déplacement
    public bool isAlive;         // État de vie

    public void Maj(Vector3 playerPos, ref BodyReference playerBody);
    // IA : poursuite du joueur, attaque au contact, knockback
}
```

**Responsabilités** :
- IA de poursuite du joueur
- Système d'attaque avec cooldown
- Knockback physique sur le joueur
- Gestion de la mort et suppression physique

### Weapon

```csharp
public class Weapon
{
    public string name;          // Nom de l'arme
    public int damage;           // Dégâts par tir
    public int range;            // Portée
    public float fireRate;       // Cadence de tir
    public int maxammo;          // Munitions maximales
    public int ammo;             // Munitions actuelles
    public Model modelname;      // Modèle 3D
    public Sound soundname;      // Son de tir

    public bool Shoot(Vector3 direction, Camera3D camera, ...);
    // Tir avec recul physique et détection de collision
}
```

**Armes Disponibles** :
- **Sniper** : Tir précis, longue portée, fort recul
- **Shotgun** : Tir rapproché, dégâts de zone
- **Pistol/Revolver** : Tir standard, équilibré
- **Bazooka** : Tir explosif, fort recul
- **Sword/Karambit** : Arme de mêlée, tir instantané

### EffetClic

```csharp
public class EffetClic
{
    public Vector2 Position;     // Position du clic
    public Texture2D Texture;    // Texture d'effet
    public int Opacite;          // Transparence (fade out)
}
```

**Responsabilités** :
- Effets visuels lors des clics souris
- Animation de fondu
- Gestion du pool d'effets

## Systèmes

### Physique (BepuPhysics)

- **Simulation** : Moteur physique principal
- **Corps Dynamiques** : Joueur et ennemis
- **Corps Statiques** : Environnement (murs, sols)
- **Capteurs** : Détection de sol, laser pour les tirs
- **Contraintes** : Frottement, élasticité, gravité

### Éclairage

- **Shader Personnalisé** : Éclairage directionnel + ambient
- **Brouillard** : Effet de distance avec paramètres configurables
- **Application** : Tous les modèles 3D utilisent le même shader

### Audio

- **Musique** : Changement automatique menu ↔ jeu
- **Volume** : Réglable (actuellement 40%)
- **Pool de Sons** : Évite les conflits audio simultanés
- **Gestion** : Chargement et libération automatique

### Interface Utilisateur

- **Menu Principal** : Boutons animés, effets de clic
- **Sélection Carte** : Choix entre tutoriel, ville, arène
- **Menu Pause** : Accessible avec Alt, options de jeu
- **HUD** : Santé, munitions, viseur

## Assets

### 2D
- Textures de boutons (play, option, quit)
- Images de cartes (aperçu)
- Effets visuels (explosions, clics)
- Viseur sniper

### 3D
- Modèles d'armes (sniper, shotgun, pistol, etc.)
- Modèle d'ennemi
- Modèles environnementaux (barils, cartes)

### Audio
- Musique de menu et de jeu
- Effets de tir par arme
- Sons d'environnement (sélection, survol)
- Sons de mort et d'attaque

## Contrôles

### Menu
- **Clic Gauche** : Interaction boutons
- **Espace** : Accès sélection carte

### Jeu
- **Souris** : Visée et rotation caméra
- **WASD** : Déplacement
- **Espace** : Saut (double saut disponible)
- **Clic Gauche** : Tir
- **R** : Recharger
- **Maj** : Accroupissement
- **Ctrl** : Dash
- **Alt** : Menu pause
- **Échap** : Quitter

## Développement

### Configuration de Build

Le projet utilise .NET 11.0 avec les paramètres suivants :
- `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` pour les pointeurs dans BepuPhysics
- `<Nullable>enable</Nullable>` pour la sécurité null
- `<ImplicitUsings>enable</ImplicitUsings>` pour les usings automatiques

### Débogage

- Mode debug avec informations de performance
- Console pour les warnings physiques
- Gestion d'exceptions pour les corps physiques invalides

### Optimisations

- Pool de sons pour éviter les conflits audio
- Gestion mémoire BepuPhysics optimisée
- Textures chargées une fois au démarrage
- Nettoyage automatique des ressources

## Licence

Ce projet est sous licence MIT. Voir le fichier LICENSE pour plus de détails.

---

Développé avec ❤️ en C# utilisant Raylib et BepuPhysics.