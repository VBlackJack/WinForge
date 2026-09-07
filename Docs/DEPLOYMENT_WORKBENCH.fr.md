# Préparation et reprise des déploiements

[English](DEPLOYMENT_WORKBENCH.md)

La page Applications propose **Aperçu du plan** et **Historique des exécutions**.
L'aperçu détecte les installations et conserve les définitions sélectionnées sans
installer de logiciel. Il présente l'action, la version détectée, l'ordre des
sources, les preuves disponibles, les besoins possibles d'élévation ou de
redémarrage et les limites du retour arrière. La détection des fonctionnalités
Windows peut nécessiter une élévation. Une erreur de détection interrompt l'aperçu
au lieu d'être interprétée comme une absence.

L'historique ouvre un reçu GUI dans `%LocalAppData%\WinForge\state`, affiche les
résultats et versions observées, puis propose de relancer uniquement les éléments
échoués, ignorés ou non tentés. Les nouvelles installations conservent leurs
définitions pour résister aux changements du catalogue. Les anciens reçus sans
définition exigent que l'application existe encore dans le catalogue. Les reçus
interrompus ou illisibles sont conservés ; la rétention existante s'applique aux
reçus terminés.

## Atelier PowerShell

Depuis la racine du dépôt, choisir un nouveau chemin pour chaque plan :

```powershell
.\Tools\Deployment-Workbench.ps1 -Action Plan -ProfileName Base -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Compare -ReferencePath .\base-plan.json -Path .\new-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Apply -Path .\base-plan.json -WhatIf
.\Tools\Deployment-Workbench.ps1 -Action Apply -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action History -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Retry -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Rollback -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Compliance -Path .\base-plan.json
```

L'application et le retour arrière modifient les logiciels et demandent confirmation.
Le plan doit être exécuté sur sa machine et son compte d'origine. Un verrou exclusif
empêche deux processus d'utiliser simultanément le même reçu. Chaque tentative est
enregistrée atomiquement avant et après l'installation. Une interruption conserve
une entrée incertaine ; la reprise réévalue l'installation avant de poursuivre.
Un logiciel découvert après interruption n'est pas attribué au journal pour un
retour arrière automatique.

Le retour arrière désinstalle uniquement les nouveaux paquets Winget ou Chocolatey
identifiés par les succès de ce plan. Les logiciels préexistants sont préservés.
Les autres méthodes nécessitent une reprise manuelle. Une désinstallation échouée
conserve son reçu pour une nouvelle tentative. Les anciennes versions et paramètres
Windows ne sont pas restaurés. Les reçus GUI et les plans PowerShell ont des schémas
distincts : utiliser leur interface respective.

## Reproductibilité et preuves

Les plans conservent l'origine héritée des applications, les définitions résolues,
la version du profil et la configuration système. La comparaison signale les
changements de définition, de version observée et de configuration. L'atelier
applique uniquement les logiciels ; la configuration système sert à la comparaison.
Le contrôle de conformité compare la détection actuelle à la référence observée
et signale explicitement les versions inconnues.

Les versions distantes restent variables, sauf si la définition contient :

```json
"SourceLock": {
  "Method": "Winget",
  "Identifier": "7zip.7zip",
  "Version": "24.09"
}
```

Le verrouillage Winget ou Chocolatey exige l'identifiant exact et interdit le repli
vers une autre source. Il est incompatible avec une méthode personnalisée. Une
version déjà installée différente ou inconnue nécessite une réconciliation, sans
mise à niveau ou rétrogradation silencieuse. Le verrouillage ne garantit ni la
disponibilité future du paquet chez l'éditeur, ni l'identité des octets téléchargés.

`SourceEvidence` concerne un identifiant de source précis. Découverte du paquet,
vérification du téléchargement et essai d'installation sont des observations datées
distinctes. Les observations absentes restent **Non mesuré**. L'ancien indicateur
`Verified` ne constitue pas une preuve d'installation. Un changement d'identifiant
invalide les anciennes preuves. Une source explicitement indisponible se distingue
d'une source non testée.

## Recette dans une VM jetable

`Tools/Test-GuestAcceptance.ps1` fonctionne uniquement avec élévation dans une VM
VMware dont le nom correspond exactement à `ExpectedComputerName`. Partir d'un
instantané propre sans 7-Zip et avec Telnet Client désactivé. Le script installe une
ancienne version donnée de 7-Zip, vérifie la mise à jour, le retour arrière partiel
et une tâche planifiée réellement exécutée sous SYSTEM. Telnet reste activé pour
représenter le cas non pris en charge par le retour arrière.

```powershell
.\Tools\Test-GuestAcceptance.ps1 -ExpectedComputerName WINFORGE-LAB `
    -ReportPath C:\WinForgeReports\run-01\acceptance.json
```

Exécuter exclusivement dans la VM jetable. Le script ne crée ni ne restaure
l'instantané. Le média Windows fourni détermine la référence : un média modifié
ne valide pas le comportement sur un média Microsoft propre.
`Tests/DeploymentExecution.Tests.ps1` tue et relance séparément un vrai processus
avec des installateurs inertes, sans modifier les logiciels de l'hôte.

## Prototype limité de configuration WinGet

`Tools/Test-WinGetConfiguration.ps1` lance `winget configure test` sur une
configuration examinée et conserve son empreinte, le code de sortie natif et la
sortie. Il n'applique aucune configuration. WinGet peut télécharger des modules
de ressources et exécuter leurs méthodes Test : examiner leurs éditeurs au préalable.
Ce prototype facultatif ne remplace pas le moteur de déploiement.

## Validation de la version et prérequis

Les déploiements SYSTEM nécessitent PowerShell 7 installé pour toute la machine par
MSI. Une installation MSIX utilisateur ne remplace pas ce prérequis. Les tâches
utilisent le chemin complet de l’exécutable et signalent explicitement son absence.
Les commandes natives avec délai maximal prennent également en charge Windows
PowerShell 5.1, y compris les arguments contenant des guillemets.

La recette Windows 11 Pro du 7 septembre 2026 a validé l’installation de 7-Zip 24.09,
la mise à jour vers la version observée 26.02.00.0, le retour arrière partiel et
l’exécution sous SYSTEM avec une copie protégée du profil. La tâche a renvoyé le
code 0. Le retour arrière retire 7-Zip et conserve volontairement Telnet, méthode
non prise en charge ; le nettoyage du laboratoire peut ensuite désactiver Telnet.
Ces résultats ne constituent pas une certification de tout le catalogue.

Utiliser `Get-ApplicationsInstallationStatus -Refresh` pour comparer les versions
immédiatement après une modification. Une version en cache ne prouve pas une mise
à jour. Le prototype `Tools/Test-WinGetConfiguration.ps1` nécessite l’activation
explicite des fonctions de configuration par `winget configure --enable` et exécute
uniquement les tests des ressources.

Les historiques GUI et plans CLI restent distincts. La restauration des anciennes
versions, celle de la configuration système et une recette exhaustive lecteur
d’écran/DPI dépassent le contrat de rollback validé. L’interruption et la reprise
sont testées avec des installateurs de recette isolés ; un installateur tiers peut
laisser des modifications partielles nécessitant sa propre procédure de réparation.

Validation complémentaire : le processus de déploiement a été interrompu pendant
l'exécution de son enfant winget.exe. La reprise a terminé le deuxième élément en
deux tentatives, sans rejouer la fonctionnalité Windows déjà réussie (une tentative).
Cela ne garantit pas l'annulation transactionnelle des modifications internes d'un
installateur tiers quelconque.
