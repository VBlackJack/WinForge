# Documentation de l’API WinForge

[English](API_DOCUMENTATION.md)

## Présentation

WinForge expose une API REST locale pour l’interface et l’automatisation. Adresse par défaut : `http://localhost:5170/`.

L’authentification utilise l’en-tête `X-API-Key`. Les opérations modificatrices exigent aussi `X-CSRF-Token`.

## Points d’accès

| Méthode et chemin | Résultat |
|---|---|
| `GET /api/version` | Version `YYYYMMDDxx` provenant de `Config/version.json` |
| `GET /api/profiles` | Profils disponibles |
| `GET /api/applications` | Catalogue applicatif |
| `GET /api/status` | État du serveur et du déploiement |
| `GET /api/cache/stats` | Statistiques du cache |
| `GET /api/csrf-token` | Jeton CSRF pour la clé courante |
| `POST /api/deploy` | Démarrage d’un déploiement |
| `POST /api/rollback` | Demande de restauration |

## Sécurité par défaut

L’écoute est locale et l’authentification par clé est activée. Les jetons CSRF sont liés à la clé qui les a obtenus et utilisables une seule fois.

La limitation de débit s’applique par adresse IP, puis par clé authentifiée. Le quota IP est partagé par les processus locaux sur un serveur écoutant uniquement localhost. Un dépassement retourne `429` avec `Retry-After`. Des échecs répétés d’authentification bloquent l’IP pendant `blockDurationMinutes` avec `403` et `AUTH_BLOCKED`.

La taille des corps est bornée par `maxRequestBodyBytes`, soit 5 MB par défaut, indépendamment de `Content-Length`. Les requêtes trop grandes échouent avec `HANDLER_ERROR`.

## Cycle de déploiement

Envoyez un corps JSON tel que `{"profile":"Base","testMode":true}`, avec la clé et un jeton CSRF à usage unique. Les schémas requis doivent être présents. Une réponse positive signifie qu’un processus de travail a démarré, pas que le déploiement a réussi.

Interrogez `GET /api/status` pour suivre `Running`, `Completed` ou `Failed`. Un second déploiement est refusé pendant le démarrage ou l’exécution. La réussite exige un résultat explicite du script. La progression vaut actuellement 0 pendant l’exécution et 100 à la réussite ; ce processus ne diffuse pas la progression par application.

## Configuration

Les paramètres se trouvent dans `Config/api-settings.json`. Les commandes de `Core/RestApiServer.psm1` gèrent les clés stockées avec DPAPI.
