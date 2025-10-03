# Win11Forge - Outils de Validation

Ce dossier contient les scripts utilitaires pour tester et valider le framework.

## Scripts Disponibles

### ProfileCreator.html ⭐ (NOUVEAU)
**Usage** : Créateur de profils JSON personnalisés

```
Double-clic sur ProfileCreator.html
OU
Ouvrir dans le navigateur (file://)
```

**Fonction** :
- Interface web standalone (aucun serveur requis)
- Création guidée en 6 étapes
- Héritage de profils existants
- Sélection d'applications prédéfinies
- **Ajout d'applications personnalisées avec sources** (Winget/Choco/Store/URL)
- Configuration système complète
- Aperçu JSON et téléchargement

**Nouveauté v2.1.3** : Support des applications personnalisées avec sources multiples !

---

### Debug-FailedApps.ps1
**Usage** : Validation des IDs d'applications

```powershell
.\Tools\Debug-FailedApps.ps1
```

**Fonction** :
- Teste tous les IDs Winget/Chocolatey/Store des applications corrigées
- Vérifie la disponibilité dans les dépôts
- Affiche un rapport de validation coloré
- Calcule le taux de succès

**Résultat attendu** : 100% de validation

---

### Test-BattleNet-Silent.ps1
**Usage** : Test d'installation silencieuse de Battle.net

```powershell
# Requires Administrator
.\Tools\Test-BattleNet-Silent.ps1
```

**Fonction** :
- Télécharge l'installeur Battle.net
- Teste 3 configurations de switches silencieux
- Valide l'installation sans interaction utilisateur
- Recommande les meilleurs paramètres

**Résultat** : Installation 100% silencieuse confirmée

---

## Quand Utiliser Ces Scripts ?

**Debug-FailedApps.ps1** :
- Avant un déploiement complet
- Après modification des profils JSON
- Pour vérifier la disponibilité des packages

**Test-BattleNet-Silent.ps1** :
- Pour valider Battle.net sur une nouvelle machine
- Après mise à jour de Battle.net
- Pour tester différents switchs d'installation

---

## Résultats de Validation (v2.1.3)

✅ **Debug-FailedApps.ps1** : 10/10 tests passés (100%)
✅ **Test-BattleNet-Silent.ps1** : Installation silencieuse confirmée

---

**Version** : 2.1.3
**Dernière validation** : 2025-10-03
