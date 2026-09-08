# Journal des modifications WinForge

[English](CHANGELOG.md)

La version de référence est définie dans `Config/version.json`. Ce document présente les changements en français. Les anciennes versions sont résumées ; leurs détails et mesures d’origine restent consultables dans l’historique Git.

## [2026090801] - 2026-09-08

### Corrigé

- Améliorer la lisibilité des badges Folio/Parchment et restaurer les couleurs après un changement de thème ou de contraste élevé.
- Garder les actions critiques accessibles à 150 % et placer l'éditeur et les boutons de récupération dans la zone de travail.
- Donner des noms compréhensibles aux éléments prononcés par NVDA et annoncer la progression et les erreurs.
- Vérifier l'absence réelle d'une application avant de déclarer son retour arrière réussi.

### Modifié

- Réorganiser les README autour du téléchargement, de la première installation, des profils et des usages courants.
- Documenter les 40 combinaisons visuelles, les 10 contrôles de thèmes clairs et les parcours NVDA qualifiés.
- Publier les preuves du premier lot catalogue : 21 essais, 20 versions détectées, détection VLC incomplète ; pas de certification des 195 applications.
- Distinguer l'installation d'une application de virtualisation de son fonctionnement, hors du périmètre de la VM de recette.

## [2026090703] - 2026-09-07

### Ajouts

- Aperçu des déploiements avec preuves par source et limites des droits, redémarrages et retours arrière.
- Définitions conservées, versions observées et reprise ciblée dans l'historique GUI.
- Atelier PowerShell avec verrouillage des versions, comparaison, reçus atomiques, reprise et retour arrière par plan.
- Harnais de recette en VM VMware jetable et prototype facultatif de test de configuration WinGet.
- Test de régression avec interruption réelle et rapport de déploiement accessible et sélectionnable.


### Corrections
- Compatibilité des arguments natifs et délais sous PowerShell 5.1 ; séparation entre version verrouillée et version observée.
- Actualisation des versions après installation et chemin PowerShell 7 explicite pour les tâches SYSTEM.
- Inclusion de l'atelier dans le ZIP et séparation de ses reçus de rollback du journal historique.
- Arguments PowerShell natifs valides pour les déploiements planifiés et copie des profils utilisateur avec leur héritage dans un stockage partagé protégé.
- Enregistrement des nouvelles installations dans un journal partagé entre les parcours séquentiel et parallèle ; exclusion des applications préexistantes, y compris lors d'une réinstallation forcée.
- Conservation des entrées dont le rollback échoue, utilisation des identifiants canoniques et restitution des résultats réels.
- Écriture atomique de l'état de reprise PowerShell et propagation des erreurs de stockage.
- Alignement des ressources XAML sur la langue sélectionnée pendant l'initialisation asynchrone.
- Exclusion des identifiants Microsoft Store des contrôles de fraîcheur du dépôt communautaire Winget et correction de cinq identifiants du catalogue. Retrait de trois sources Winget indisponibles lorsqu'un autre canal est déjà déclaré ; LDPlayer reste indisponible dans les sources interrogées.

## [2026090702] - 2026-09-07

### Corrections
- Raccordement du dialogue de sauvegarde des profils, validation des destinations, protection contre le remplacement accidentel et conservation des métadonnées existantes.
- Conservation des historiques corrompus, écriture atomique et signalement des erreurs de stockage.
- Propagation des erreurs de points de reprise et conservation du point original jusqu'au succès de la reprise.
- Annulation rapide des lectures PowerShell et nettoyage des lancements de tests graphiques échoués.

### Changements
- Confinement des fabriques de chemins à leurs racines et cache de découverte PowerShell propre à chaque instance.
- Tests de régression pour l'annulation, le stockage corrompu, les destinations verrouillées et les noms de profils.

## [2026090601] - 2026-09-06

### Corrections

- Chargement et hooks des plugins isolés de la session principale, à partir d’une copie immuable du module validé ; désinscription des hooks sans modifier une collection en cours d’énumération.
- Conservation de SystemConfig et des propriétés JSON personnalisées lors de la sauvegarde et de la mise à jour des profils, avec remplacement atomique du fichier.
- Respect des délais des processus pendant la lecture simultanée des sorties standard et d’erreur.
- Validation des corps JSON de l’API en mémoire, refus si un schéma requis manque, exécution du déploiement dans un processus de travail, refus des chevauchements et suivi de réussite ou d’échec.
- Inclusion des schémas et des documents dans les deux langues dans les paquets ; contrôle des entrées avant création des sommes de contrôle.

### Compilation et documentation

- Pester fixé à 5.7.1 et assertions de doublures compatibles.
- Validation CI indépendante d’ICMP et des essais d’écriture sur l’hôte.
- Commit ThemeForge fixé dans une configuration commune à la CI et au script local.
- Documentation anglaise par défaut, versions françaises séparées et liens réciproques.

## [2026081201] - 2026-08-12

Renforcement des listes d’exécutables et d’arguments pour la détection, des types autorisés dans les plugins, du mode ConstrainedLanguage, de la vérification d’éditeur et du contrôle d’empreinte avant import. L’API ajoute la limitation des corps, le contrôle par clé et la validation des handlers.

La version réactive les règles d’analyse, valide les paramètres et les configurations, dérive les délais de la configuration et retire un ancien wrapper de processus. Elle corrige les valeurs JSON nulles, la restauration des langues dans les tests, les courses de chargement des paramètres, l’encodage PowerShell 5.1 et plusieurs noms accessibles.

Les seuils de couverture sont fixés à 38 % pour .NET et 48 % pour Pester. Ces changements décrivent cette version historique ; les corrections complémentaires de l’audit figurent dans Unreleased. Les [notes françaises de cette version](Docs/releases/v2026081201.fr.md) détaillent les changements.

## [2026062701] - 2026-06-27

Listes de contrôle partagées pour la détection et les plugins, métadonnées de confiance des téléchargements directs, mises à jour de la CI, ressources localisées, découpage des services et découplage des ViewModels. Correction des diagnostics de requêtes de version, des rechargements du catalogue et de l’exposition des traces d’exception. Les tests de configuration système remplacent les opérations Explorer et DNS par des doublures.

## [2026062301] - 2026-06-23

Renommage public de Win11Forge en WinForge avec migration des données locales et compatibilité des tâches planifiées. Clarification des analyses de mises à jour et des prérequis, journaux en anglais et UTF-8. Correction des analyses du tableau de bord, de la mise à jour Chocolatey, des choix installer/mettre à jour, de l’invalidation des caches et des versions à zéros finaux.

## [2026062201] - 2026-06-22

Consolidation de l’interface WPF, des thèmes et des états désactivés. Amélioration du contraste, de l’alignement des contrôles, de la navigation, des sélections et de la gestion des profils. Renforcement des contrôles des téléchargements directs, de la validation et de la documentation. Les détails des différents lots restent dans le journal anglais et les révisions associées.

## [3.7.2] - 2026-02-12

Mode asynchrone de l’API, éditeur d’applications avec recherche de sources, validation des paquets, visionneuse de journaux, import/export des paramètres, interface de planification, plugins isolés et journaux structurés.

## [3.6.7] - 2026-02-05

Correction de l’attente Office : le service permanent OfficeClickToRun ne doit pas être considéré comme une installation en cours. Ajustement du délai de l’interface, extraction des noms de téléchargement depuis les paramètres d’URL et filtrage des journaux binaires.

## [3.5.2] - 2026-01-28

Correction du nom Clear-LogBuffer dans les tests de journaux structurés et actualisation de la documentation du profil Enterprise, de l’API et des déploiements planifiés. La version enregistrait 1047 tests réussis.

## [3.5.1] - 2026-01-24

Mise à jour du catalogue, correction du compteur à 175 applications, amélioration de la détection des environnements d’exécution et des règles de validation JSON.

## [3.5.0] - 2026-01-21

Service de détection fondé sur le JSON pour Command, Registry, File et WindowsFeature, résolution des exécutables et correspondance des identifiants WinGet. Correction de la détection des environnements .NET, VC++ et Java, et séparation des interfaces.

## [3.2.3] - 2026-01-19

Détection prioritaire dans le registre avec cache de cinq minutes, invalidation manuelle et cache des mises à jour par lots de dix minutes. Les gains chiffrés du journal anglais sont des mesures historiques.

## [3.2.2] - 2026-01-19

Tableau de bord organisé autour des états Checking, Ready et Update ; activation des commandes selon l’état et version lue depuis la configuration. Refactorisation du moteur d’installation.

## [3.2.0] - 2026-01-17

Simplification de la navigation, retrait de l’éditeur de profils redondant et nettoyage du catalogue applicatif.

## [3.1.4] - 2026-01-16

Protection contre les injections dans la restauration, passage sûr des arguments de désinstallation et validation des fichiers d’état, identifiants de session, noms de profils et d’applications.

## [3.1.3] - 2026-01-16

Contrôle des traversées de chemins avant et après expansion des variables d’environnement. Refus par défaut des domaines de téléchargement non autorisés, avec surcharge explicite disponible.

## [3.1.2] - 2026-01-16

Affichage continu des journaux d’installation, téléchargement de secours par curl et correction de la source Battle.net et de plusieurs détections.

## [3.0.0] - 2026-01-05

Introduction de l’interface WPF moderne. Amélioration de la fiabilité du moteur : accès SHA256 en mode strict, reconnaissance des paquets déjà installés, traitement des mises à jour absentes et correction du téléchargement WebClient. Renforcement de la localisation.

## [2.4.0] - 2025-10-06

Amélioration de la compatibilité PowerShell 5.1, de la stabilité séquentielle et des performances de System-Audit. Intervalle par défaut porté à cinq secondes, fréquences de collecte ajustées et option SkipApplicationMonitoring.

Ajout du lanceur TrustedInstaller, correction des chemins avec espaces, des propriétés sous StrictMode, du redémarrage PowerShell 7, des statistiques parallèles, des priorités et booléens, des téléchargements directs et des archives ZIP. Uniformisation de l’encodage et des versions documentées. Les mesures d’origine restent dans le journal anglais.

## [2.3.0] - 2025-10-04

Ajout de StartMenuPinning, StartMenuLayout, StartupManager et des journaux individuels parallèles. Détection StoreApp via winget pour éviter les conflits Appx de PowerShell 7. Corrections de WhatsApp, Quick Assist, Epic Games, Proton et CUE Splitter, ainsi que de l’accès à InstallArguments en mode strict.

La validation historique utilisait quatre profils, 66 applications et cinq workers parallèles. Aucun changement de format ni migration n’était requis depuis 2.2.0.

## [2.2.0] - 2025-10-03

Refonte autour d’une base de 66 applications et de profils compacts par AppId. Ajout du module ApplicationDatabase, de l’interface PowerShell, de ProfileCreator, de la recherche de sources et des lanceurs avec élévation.

Migration des profils 2.0/2.1 avec sauvegardes, amélioration de l’héritage et des choix de navigation, ajout de Creality Slicer, regroupement des outils et archivage des fichiers obsolètes. Correction des propriétés, du comptage, des sources et du retour à l’interface après déploiement. Les anciens scripts de migration doivent être consultés dans leur révision historique.

## [2.1.3] - 2025-10-03

Correction des arguments silencieux Battle.net et des identifiants WhatsApp et Proton. Vérification des sources Google Drive et PDF-XChange Editor, avec améliorations de détection et d’installation.

## [2.1.2] - 2025-10-02

Correction des appels Write-Log vides, du compteur d’héritage, de PowerToys et de Quick Assist. Ajout des installations parallèles et de la détection de PowerShell 7.

## [2.1.1] - 2025-10-01

Correction des tableaux DNS et de la gestion des erreurs de la barre des tâches.

## [2.1.0] - 2025-10-01

Ajout du mode Parallel et de MaxParallelJobs, de la journalisation du mode et du suivi de progression parallèle.

## [2.0.2] - 2025-09-30

Correction des priorités du profil Base et de plusieurs méthodes de détection.

## [2.0.0] - 2025-09-30

Architecture modulaire, héritage Base/Office/Gaming/Personnel, installation multisource, détection d’environnement et journaux avec rapports.
