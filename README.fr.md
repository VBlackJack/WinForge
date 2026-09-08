# WinForge

[English](README.md) · [Télécharger](https://github.com/VBlackJack/WinForge/releases/latest) · [Guide utilisateur](Docs/USER_GUIDE.fr.md)

**Choisissez vos applications, prévisualisez les changements et préparez votre PC Windows.**

WinForge réunit l'installation, les mises à jour et les profils réutilisables dans une interface de bureau. Partez d'un profil prêt à l'emploi ou sélectionnez uniquement les applications utiles, puis suivez les opérations et leurs journaux.

Version **2026090801** · Windows 10/11 · Français et anglais · Apache 2.0

## Télécharger et démarrer

1. Ouvrez la [dernière release](https://github.com/VBlackJack/WinForge/releases/latest).
2. Dans **Assets**, téléchargez `WinForge_v2026090801.zip` et son fichier `.zip.sha256`. Prenez le ZIP de WinForge, plutôt que les archives **Source code** de GitHub.
3. [Vérifiez le téléchargement](#vérifier-le-téléchargement), puis extrayez tout le ZIP dans un dossier local.
4. Ouvrez ce dossier et double-cliquez sur **WinForge.cmd**. Conservez les fichiers fournis ensemble.
5. Ouvrez **Applications** pour choisir votre première installation.

L'interface distribuée inclut son environnement .NET : le SDK .NET n'est pas nécessaire. Une connexion Internet est requise pour télécharger les paquets. Certaines opérations demandent les droits administrateur. Windows 10 21H2 ou ultérieur, ou Windows 11, est nécessaire.

## Votre première installation

1. Choisissez un **profil**, ou cochez individuellement les applications de la liste.
2. Lancez l'analyse des applications installées pour actualiser l'état de votre PC.
3. Vérifiez les applications cochées. Utilisez la recherche et les filtres pour affiner la sélection.
4. Ouvrez l'aperçu du plan pour examiner les opérations proposées et leurs limites.
5. Lancez l'installation de la sélection et suivez sa progression.
6. Consultez l'historique d'exécution pour examiner le résultat. En cas d'échec, lisez le message et les journaux avant de réessayer les applications concernées.

Pour découvrir WinForge, commencez par quelques applications avant d'appliquer un profil complet.

## Choisir un profil de départ

| Profil | Usage conseillé |
| --- | --- |
| **Base** | Navigation, multimédia et utilitaires du quotidien |
| **Office** | Bureautique, documents, PDF et collaboration |
| **Gaming** | Plateformes de jeux et communication |
| **Personnel** | Développement et poste personnel avancé |
| **Enterprise** | Outils IT, sécurité et poste professionnel |

Un profil peut inclure des applications héritées d'un parent. Vérifiez la sélection complète avant l'installation : le profil est un point de départ, pas une obligation.

Pour réutiliser votre sélection, enregistrez-la comme nouveau profil. Pour modifier le profil sélectionné, ajustez les cases puis utilisez sa commande de mise à jour. Les applications héritées doivent être retirées du profil parent. [Comprendre les profils](Docs/USER_GUIDE.fr.md).

## Les usages courants

| Je veux… | Où commencer |
| --- | --- |
| Rechercher les mises à jour | **Applications**, puis l'analyse des mises à jour |
| Retrouver le résultat d'une installation | L'historique d'exécution dans **Applications**, puis les **journaux** pour le détail |
| Changer le thème, la langue ou les options d'accessibilité | **Paramètres** |
| Ajouter ou corriger une définition d'application | Le **catalogue d'applications** |
| Automatiser des déploiements avec PowerShell | [Atelier de déploiement](Docs/DEPLOYMENT_WORKBENCH.fr.md) |

## Ce qu'il faut savoir

- Le catalogue contient **195 applications**. Les sources et la prise en charge varient selon l'application ; le catalogue n'est pas entièrement certifié.
- Installation et validation du fonctionnement sont distinctes. Le fonctionnement des applications de virtualisation est hors du périmètre des essais dans notre VM jetable. Il doit être vérifié sur du matériel physique adapté ou une configuration de virtualisation imbriquée prise en charge.
- Le retour arrière peut supprimer les nouveaux paquets pris en charge. Il ne restaure ni une ancienne version d'application ni la configuration Windows.
- Les reçus de l'interface et les plans de déploiement PowerShell utilisent des formats distincts.
- Le [rapport de validation](Docs/Validation/20260908/README.fr.md) décrit les affichages, parcours vocaux et installations testés, ainsi que les écarts connus, dont la détection de version VLC et les sources LDPlayer indisponibles.

## Mettre WinForge à jour

Fermez WinForge et extrayez la nouvelle release dans un autre dossier. Conservez l'ancien dossier et vos profils personnalisés jusqu'à la vérification de la nouvelle version. Consultez le [journal des changements](CHANGELOG.fr.md).

## Vérifier le téléchargement

Ouvrez PowerShell dans le dossier contenant les deux fichiers téléchargés, puis exécutez :

```powershell
$expected = ((Get-Content .\WinForge_v2026090801.zip.sha256 -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash .\WinForge_v2026090801.zip -Algorithm SHA256).Hash
if ($expected -eq $actual) { 'OK' } else { 'CHECKSUM MISMATCH' }
```

Extrayez et lancez WinForge uniquement si le résultat est **OK**. Sinon, téléchargez à nouveau les deux fichiers depuis la même release.

## Aide et documentation

- [Guide utilisateur](Docs/USER_GUIDE.fr.md) : profils, déploiement et dépannage.
- [Index de la documentation](Docs/README.fr.md) : administration, PowerShell et développement.
- [Signaler un problème](https://github.com/VBlackJack/WinForge/issues) : indiquez les versions de WinForge et Windows, l'application et le message d'erreur. Retirez les secrets des journaux partagés.
- [Contribuer](CONTRIBUTING.md) · [Licence](LICENSE)
