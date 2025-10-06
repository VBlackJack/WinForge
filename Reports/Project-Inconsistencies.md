# Rapport d'analyse des incohérences - Win11Forge v2.4.0

## Méthodologie
- Développement d'un outil Python (`Tools/analyze_inconsistencies.py`) pour vérifier la base applicative, les profils et les métadonnées associées.
- Chargement de la base `applications.json` ainsi que des profils (`Profiles/*.json`) pour reconstruire les héritages et contrôler la cohérence des statistiques déclarées.
- Vérification automatique exécutée via `python Tools/analyze_inconsistencies.py`.

## Résultats clés
1. **✅ RÉSOLU - Identifiant Microsoft Store en double**
   Les applications _PowerToys_ et _ShareX_ déclaraient toutes deux l'identifiant Microsoft Store `9NBLGGH4Z1SP`.
   _Correction appliquée (commit 993a718):_
   - PowerToys: `9NBLGGH4Z1SP` → `XP89DCGQ3K6VLD` (ID vérifié sur Microsoft Store 2025)
   - ShareX conserve `9NBLGGH4Z1SP` (ID correct vérifié)
   - Fichiers corrigés: `Apps/Database/applications.json:278` et `Tools/applications-data.js:1`

2. **✅ RÉSOLU - Paramètre `DefaultRequired` incohérent pour QuickAssist**
   La base JSON marquait `QuickAssist` comme optionnelle (`DefaultRequired: false`) tandis que `applications-data.js` la forçait en installation (`DefaultRequired: true`).
   _Correction appliquée (commit 993a718):_
   - `applications-data.js`: `DefaultRequired: true` → `false` (aligné avec applications.json:390)
   - Les deux sources de données sont maintenant synchronisées

3. **Priorités d'installation discontinues**
   Les priorités par défaut ne sont pas continues : aucun paquet n'occupe les valeurs 25, puis 56 à 57, ni 69 à 98 alors que la dernière application utilise l'indice 99. Cela peut générer des trous dans les tableaux de bord ou dans les flux d'installation qui s'attendent à une numérotation dense.
   _Action recommandée :_ réattribuer les priorités pour combler les gaps ou documenter que ces valeurs sont volontairement réservées.

4. **Profils cohérents avec la base**
   Les profils Base, Office, Gaming et Personnel résolvent respectivement 30, 35, 39 et 64 applications uniques après prise en compte des héritages. Ces valeurs correspondent à la documentation du projet.
   _Action recommandée :_ aucune action nécessaire, les profils sont cohérents avec la base.

5. **Statistiques de catégories synchronisées**
   Le décompte des catégories renseigné dans la base (`Categories`) correspond exactement aux affectations réelles des applications (somme totale 66). Il n'y a pas de catégorie orpheline ni de catégorie déclarée sans application.
   _Action recommandée :_ maintenir la mise à jour conjointe des applications et des métadonnées lors des prochaines évolutions.

## Points de contrôle supplémentaires
- Les jeux de données JavaScript (`Tools/applications-data.js`) et JSON (`Apps/Database/applications.json`) contiennent exactement le même ensemble de 66 applications, mais `QuickAssist` doit être alignée sur le même `DefaultRequired`.
- Les tags déclarés dans la base disposent tous d'une définition dans la section `Tags`.
- `WindowsSandbox` n'a pas de source de déploiement déclarée (information signalée à titre indicatif).
- Aucune date `LastVerified` ne dépasse la date d'exécution de l'analyse.

## Suivi
Ce rapport peut être ré-exécuté à tout moment avec :
```bash
python Tools/analyze_inconsistencies.py
```
Le script retournera la liste actualisée des avertissements et des statistiques associées.