# Recette visuelle de l’interface en machine virtuelle

[English](GUI_VM_VISUAL_CHECKLIST.md)

## Périmètre et matrice

La [recette locale du 2026-09-08](Validation/20260908/README.fr.md) a validé les
40 combinaisons ci-dessous sur les candidats locaux non publiés identifiés par cas. Les parcours
vocaux NVDA enregistrés sont qualifiés séparément, sans certification globale
de chaque contrôle ou aide technique.

Contrôler la persistance des thèmes, la lisibilité, le contraste élevé, la réduction des animations, le redimensionnement et les états des actions.

Tester `1366x768` à `100%`, `125%` et `150%`, puis `1920x1080` à `100%` et `125%`. Pour chaque combinaison, vérifier Folio (clair) et Drakul (sombre), le contraste élevé activé et désactivé, ainsi que la réduction des animations activée et désactivée.

## Démarrage et disposition

1. Lancer l’application et vérifier le thème enregistré.
2. Ouvrir les paramètres et vérifier l’absence de changement spontané.
3. Redémarrer et vérifier la persistance du thème, du contraste et des animations.
4. Parcourir Dashboard, Prerequisites, Apps, Deployment, Settings, Logs et App Catalog.
5. Réduire puis rétablir la largeur de chaque page.

Résultat attendu : aucune action importante coupée ou inaccessible, aucun chevauchement et aucun défilement horizontal hors des zones prévues.

## Contrôles par page

| Page | Vérifications |
|---|---|
| Apps | Filtres et actions renvoyés à la ligne à faible largeur, défilement vertical utilisable, analyse et mise à jour accessibles, boutons de progression alignés |
| Apps | Pause/reprise uniquement pour les lots compatibles ; masquées pendant une mise à jour |
| Apps | Mise à jour du profil visible après modification de la sélection, distincte de l’enregistrement |
| Logs | Filtres, effacement et suppression accessibles à `1366x768` et `150%` |
| Settings | Thème, animations et contraste appliqués immédiatement et conservés après redémarrage |
| Deployment | Visionneuse de journaux et boutons entièrement accessibles à la taille minimale |

## Accessibilité

Parcourir les actions avec Tab, Maj+Tab, Entrée et Espace. Vérifier le focus visible et les noms accessibles des éditeurs de détection, de téléchargement direct et de source Store. Contrôler la lisibilité des badges et des états en thème clair.

## Régressions

1. Rouvrir la dernière page visitée.
2. Tester Annuler/Rétablir dans l’en-tête.
3. Vérifier les catégories et la recherche du sélecteur d’applications à faible largeur.
4. Vérifier les filtres dans Logs et Apps.
5. Appliquer un profil, modifier la sélection et changer de profil : vérifier Remplacer/Fusionner/Annuler.
6. Mettre à jour un profil, le sélectionner à nouveau et vérifier la sélection enregistrée.
7. Enregistrer une entrée inchangée du catalogue et vérifier la conservation des métadonnées.

## Captures WinSight

```powershell
pwsh -NoProfile -File Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot <path-to-winsight>
```

Captures attendues :

- `TestResults\winsight\01-dashboard.png`
- `TestResults\winsight\02-settings.png`
- `TestResults\winsight\03-app-catalog.png`

## Fiche d’anomalie et sortie

Pour chaque anomalie, indiquer un identifiant `GUI-VM-###`, l’environnement, la page, les étapes, le résultat obtenu, le résultat attendu, la gravité et la capture.

La recette est terminée lorsqu’aucune anomalie critique ne subsiste, que les paramètres visuels persistent et que les actions importantes, les noms accessibles et le focus sont utilisables dans toute la matrice.
