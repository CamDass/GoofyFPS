import os
import subprocess

def generer_contenu():
    fichier_sortie = "contenu.txt"
    
    # 1. Récupération de l'arborescence (commande Windows 'tree /f /A')
    # Le /A permet d'utiliser des caractères ASCII standard pour éviter les problèmes d'encodage
    try:
        tree_output = subprocess.check_output("tree /f /A", shell=True, text=True, stderr=subprocess.STDOUT)
    except subprocess.CalledProcessError as e:
        tree_output = f"Erreur lors de l'exécution de la commande tree : {e.output}"
        
    # 2. Récupérer uniquement les fichiers .cs à la racine (ignore les sous-dossiers)
    fichiers_cs = [f for f in os.listdir('.') if os.path.isfile(f) and f.endswith('.cs')]
    
    with open(fichier_sortie, 'w', encoding='utf-8') as f_out:
        # Écriture de l'arbre en haut du fichier
        f_out.write(tree_output)
        f_out.write("\n========\n\n")
        
        # Écriture du contenu de chaque fichier .cs
        for i, fichier in enumerate(fichiers_cs):
            f_out.write(f"{fichier}\n\n")
            
            try:
                with open(fichier, 'r', encoding='utf-8') as f_in:
                    f_out.write(f_in.read())
            except Exception as e:
                f_out.write(f"// Erreur lors de la lecture du fichier : {e}")
            
            f_out.write("\n\n")
            
            # Ajout du séparateur pour tous les fichiers sauf le dernier
            if i < len(fichiers_cs) - 1:
                f_out.write("========\n\n")
                
    print(f"✅ Fichier '{fichier_sortie}' généré avec succès !")
    print(f"📄 {len(fichiers_cs)} fichier(s) .cs trouvé(s) et intégré(s).")

if __name__ == "__main__":
    generer_contenu()