# Validation de la version 2026090703

[English](RELEASE_VALIDATION.md)

La recette VMware Windows 11 Pro a validé l'installation de 7-Zip 24.09, la mise à
jour vers la version observée 26.02.00.0, le retour arrière partiel et une tâche
SYSTEM avec code de sortie 0 et copie protégée du profil. Un processus de
 déploiement réel interrompu pendant winget.exe a repris sans rejouer le premier
élément terminé : une tentative pour celui-ci, deux pour le second.

Le prototype WinGet Configuration Test a vérifié DeveloperMode=false avec la
ressource Microsoft.Windows.Settings et renvoyé 0. Aucun Configure apply exécuté.
L'exemple est dans Config/Examples/developer-mode-disabled.winget.

Contrôles locaux : 2005 tests Pester réussis, 6 ignorés, couverture 53,94 % pour un
seuil de 48 % ; 787 tests .NET ; 5 tests UIA. La CI de publication relance la suite
complète sur la révision commitée.

## Vérification après publication, 2026-09-08

La release [v2026090703](https://github.com/VBlackJack/WinForge/releases/tag/v2026090703)
est publiée. La PR #32 est fusionnée ; main et le tag pointent sur
`84cbc5d1d78698a750132499afd3c1033d49940d`. Les CI main 34156567128 et tag
34156586036 ont réussi. Le résultat CI final est de 2011 tests Pester réussis
et 1 ignoré ; les compteurs locaux ci-dessus décrivent le passage local antérieur.

La recette locale du 2026-09-08 clôt la matrice visuelle de 40 combinaisons,
qualifie les parcours vocaux NVDA enregistrés et clôt un premier lot catalogue
de 21 essais. Les corrections concernent un candidat non publié. La couverture
des 195 applications reste partielle ; les limites ci-dessous ne promettent pas
de nouvelles fonctions.

## Limites

La [recette du 2026-09-08](Validation/20260908/README.fr.md) conserve les preuves
du lot catalogue réel, de la parole NVDA et des corrections locales. Elle distingue
le paquet publié du candidat non publié.

- Les preuves sont propres à chaque paquet ; les 195 applications ne sont pas
  toutes certifiées. LDPlayer reste indisponible dans les sources vérifiées.
- Le rollback désinstalle seulement les nouveaux paquets pris en charge. Il ne
  restaure ni anciennes versions ni configuration Windows.
- Les reçus GUI et plans CLI utilisent des formats distincts.
- UIA vérifie navigation, libellés, focus et rapport. La restitution vocale complète
  et toutes les combinaisons physiques de moniteurs/DPI ne sont pas certifiées.
- L'interruption du processus ne rend pas les installateurs tiers transactionnels ;
  leur procédure de réparation peut rester nécessaire.
- Les tâches SYSTEM nécessitent PowerShell 7 installé pour toute la machine.
