# Règles de publication

[English](PUBLICATION_POLICY.md)

## Langues

L'anglais est la langue par défaut des README, de la documentation, des changelogs et des notes de version. Conserver les traductions françaises dans des fichiers séparés, à côté des sources anglaises, avec le suffixe `.fr.md`, par exemple `README.md` et `README.fr.md`.

Ajouter des liens réciproques entre les langues et mettre les deux versions à jour ensemble. Garder les commandes, identifiants, chemins, numéros de version et sommes de contrôle identiques dans les traductions. Signaler les liens vers les pages disponibles uniquement en anglais dans la documentation française.

## Notes de version

Pour chaque prochaine version, préparer `Docs/releases/<tag>.md` et `Docs/releases/<tag>.fr.md`. Utiliser le fichier anglais comme corps de la release GitHub. Joindre le fichier français comme fichier téléchargeable séparé et ajouter un lien vers celui-ci dans le corps anglais. Ne pas regrouper les deux langues dans un même corps de release.

Vérifier les deux fichiers par rapport au même tag, aux changements, aux livrables et aux sommes de contrôle avant publication. Cette convention ne traduit pas rétroactivement les notes de version historiques.

## Règles éditoriales

- Utiliser une ponctuation simple sans cadratins.
- Décrire le produit et les comportements vérifiés, sans attribution aux outils, crédits de génération ni transcriptions de travail.
- Rédiger les messages de commit et descriptions de dépôt en anglais.
- Limiter les mots-clés GitHub aux fonctionnalités et technologies implémentées ; les revoir lorsque le périmètre du projet change.

## Relecture

Avant publication, vérifier les liens de langue, chemins relatifs, traductions, références de version, ponctuation et liens vers les fichiers de la release. Une modification documentaire n'autorise pas à elle seule une publication ni une réécriture de l'historique Git.
