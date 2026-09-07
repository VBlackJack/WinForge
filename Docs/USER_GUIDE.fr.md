# Guide utilisateur WinForge

[English](USER_GUIDE.md)

Version actuelle du framework : `2026090703`.

## Démarrage rapide

1. Extraire l'archive de la release.
2. Lancer `WinForge.cmd` ou l'exécutable de l'interface.
3. Choisir un profil ou ajuster manuellement la sélection d'applications.
4. Démarrer le déploiement et suivre sa progression.

## Profils

- `Base` : applications et utilitaires essentiels.
- `Office` : environnement de productivité.
- `Gaming` : jeux et outils de communication.
- `Personnel` : environnement de développement.
- `Enterprise` : configuration orientée sécurité.

Les profils peuvent hériter d'autres profils. Par exemple, `Gaming` s'appuie sur `Office`, et `Office` sur `Base`.

### Modifier un profil existant

1. Ouvrir `Applications`.
2. Choisir le profil dans le sélecteur `Profil`.
3. Cocher ou décocher les applications dans la grille.
4. Cliquer sur `Mettre à jour le profil`.

Le fichier du profil est mis à jour avec les applications cochées. Si le profil hérite d'un autre profil, les applications héritées restent sélectionnées car elles appartiennent au parent. Modifier le profil parent pour les retirer, ou enregistrer un nouveau profil sans ce parent.

### Créer un profil

1. Sélectionner les applications souhaitées.
2. Cliquer sur `Sauvegarder le profil`.
3. Choisir un nouveau nom et, si nécessaire, un profil parent.
4. Enregistrer.

## Parcours habituel

1. Valider les prérequis.
2. Vérifier les applications sélectionnées.
3. Lancer le déploiement.
4. Consulter les journaux si une étape échoue.
5. Utiliser le rollback si nécessaire.

## Déploiements planifiés

Ouvrir l'onglet des déploiements planifiés dans les paramètres avec les droits administrateur. Choisir un profil, un déclencheur et une date/heure, puis créer le déploiement.

La planification copie le profil utilisateur sélectionné et tous ses profils parents. Les profils utilisateur ont priorité sur les profils fournis. Les modifications ultérieures des profils ne changent pas une planification existante ; la recréer pour les prendre en compte. Conserver l'installation de WinForge à son emplacement initial pour que le lanceur planifié reste accessible.

## Rollback et reprise

Les nouvelles installations réussies sont enregistrées dès leur fin, y compris en parallèle. Les applications détectées avant l'installation sont exclues, même en cas de réinstallation forcée. Le journal persiste entre les processus et reste disponible jusqu'au rollback des entrées ou à son effacement explicite.

Le rollback automatique prend en charge Winget et Chocolatey. Les autres méthodes restent listées pour une récupération manuelle. Un échec partiel conserve les entrées en échec pour une tentative ultérieure et indique le nombre réellement désinstallé. Un fichier de reprise illisible ou impossible à écrire provoque une erreur ; conserver le fichier et résoudre le problème de stockage avant de réessayer.

## Catalogue d'applications

- Consulter et modifier la base d'applications depuis l'interface.
- Les modifications conservent les métadonnées de vérification lorsque le contenu de l'application ne change pas.
- Utiliser `Tools/Validate-AppDatabase.ps1` après une modification de la base.

## Dépannage

- Vérifier les droits administrateur pour les opérations système.
- Vérifier la connexion internet pour les sources de paquets.
- Relancer les contrôles avec `Run-All-Checks.ps1`.
- Valider la base d'applications avec `Tools/Validate-AppDatabase.ps1`.
- Exécuter le test d'interface facultatif avec `Tools/Invoke-WinsightSmoke.ps1` lors de modifications des parcours de bureau.

## Références complémentaires

- [Documentation API (anglais)](API_DOCUMENTATION.md)
- [README du projet](../README.fr.md)

## Préparation et reprise des déploiements

Voir [les aperçus, l'historique et l'atelier PowerShell](DEPLOYMENT_WORKBENCH.fr.md).
