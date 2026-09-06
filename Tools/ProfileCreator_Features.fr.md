# Créateur de profils

[English](ProfileCreator_Features.md)

Ouvrez `Tools/ProfileCreator.html` dans un navigateur. L’assistant fonctionne localement sans serveur web et lit son catalogue autonome dans `applications-data.js`.

## Parcours

1. Renseigner le nom, la description et la version du profil.
2. Choisir éventuellement un parent, comme Base ou Office.
3. Sélectionner les applications intégrées par catégorie.
4. Ajouter les applications personnalisées avec un nom, une catégorie, au moins une source et, si nécessaire, des arguments d’installation.
5. Configurer l’explorateur, la barre des tâches, le réseau, la confidentialité, les performances et la sécurité.
6. Relire le JSON généré et télécharger le profil.

Les sources personnalisées peuvent utiliser Winget, Chocolatey, Microsoft Store ou une URL directe. Supprimez les entrées inutiles avant l’export. L’aperçu et les compteurs reflètent la sélection actuelle.

## Exemple

Pour Postman, indiquez `Postman.Postman` comme identifiant Winget, `postman` comme paquet Chocolatey et `Development` comme catégorie. Vérifiez ces identifiants auprès des sources avant le déploiement.

Les profils contiennent `Name`, `Description`, `Version`, `Inherits`, `Applications` et éventuellement `SystemConfig`. L’assistant peut produire des définitions intégrées ; le catalogue d’exécution accepte aussi les références par identifiant.

## Valider et déployer

Placez le JSON téléchargé dans `Profiles/` sous un nom distinct. Contrôlez les champs de détection et de vérification des téléchargements avec les schémas livrés. La validation des champs dans le navigateur ne prouve ni l’authenticité de l’installeur ni le fonctionnement de la détection.

```powershell
Import-Module .\Modules\JsonSchemaValidation.psm1 -Force
Test-JsonAgainstSchema -JsonPath 'Profiles/MyProfile.json' -SchemaPath 'Schemas/deployment-profile.schema.json'
.\Deploy-Win11Environment.ps1 -ProfileName 'MyProfile' -TestMode
```

Examinez le résultat avant un déploiement réel sans `-TestMode`. Les contrôles actuels refusent les commandes de détection arbitraires et les téléchargements directs dépourvus des métadonnées de confiance requises.

La sélection de travail reste locale à la session du navigateur ; téléchargez le fichier pour conserver le profil. Le catalogue intégré peut différer de la base principale. Consultez la [référence du catalogue](../Apps/README.fr.md) et le [guide utilisateur](../Docs/USER_GUIDE.fr.md).
