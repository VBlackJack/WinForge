# Rapport d'analyse des incohérences - Win11Forge v2.5.0

## Méthodologie
- Exécution de l'outil Python `Tools/analyze_inconsistencies.py` (version interne 2.0) pour analyser la base applicative, les profils et les jeux de données dérivés.
- Chargement de `Apps/Database/applications.json`, des profils (`Profiles/*.json`) ainsi que du miroir JavaScript `Tools/applications-data.js`.
- Les règles de validation portent sur :
  - l'intégrité des métadonnées (`TotalApplications`, `LastUpdated`)
  - la densité et l'unicité des priorités d'installation (`DefaultPriority`)
  - la cohérence des profils (héritages, doublons, références manquantes)
  - la synchronisation des tags et catégories
  - la parité JSON ⇔ JavaScript pour les champs `DefaultPriority` / `DefaultRequired`
- L'outil retourne un code de sortie non nul si des erreurs critiques sont détectées, afin de faciliter l'intégration dans un pipeline CI/CD.

## Résultats de l'analyse (exécution du 2025-10-06)

### 🔴 Erreurs
- Aucune erreur détectée (`exit code 0`).

### 🟠 Avertissements
1. **Plages de priorités inoccupées**  
   Les valeurs suivantes ne sont utilisées par aucune application : `25, 56, 57, 69-98`.  
   _Impact_: risque de confusion dans les tableaux de bord ou d'éventuelles règles d'installation supposant une séquence dense.  
   _Recommandation_: soit réattribuer les priorités pour combler les gaps, soit documenter clairement que ces indices sont réservés.

### 🔵 Notes
- `WindowsSandbox` reste l'unique application sans source de distribution (comportement attendu : fonctionnalité Windows intégrée).
- 66 applications inventoriées avec 66 entrées de priorité valides.
- Profils résolus sans incohérences :
  - `Base` → 30 applications (30 uniques)
  - `Office` → 35 applications (35 uniques)
  - `Gaming` → 39 applications (39 uniques)
  - `Personnel` → 64 applications (64 uniques)
- `Tools/applications-data.js` est parfaitement synchronisé avec `Apps/Database/applications.json` (priorités et indicateurs `DefaultRequired`).

## Recommandations de suivi
- Mettre en place une convention officielle autour de l'espace des `DefaultPriority` (plages réservées, signification des gaps).
- Rejouer le script `python Tools/analyze_inconsistencies.py` après chaque modification de la base applicative ou des profils pour garantir la détection précoce des divergences.

---
Rapport généré automatiquement dans le cadre de l'audit Win11Forge v2.5.0.
