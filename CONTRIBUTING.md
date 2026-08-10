# Contribuer à Ultrawide Monitor

Merci de contribuer à Ultrawide Monitor. Le projet est maintenu par [Gil Breysse (RED)](https://github.com/ImRedTV).

## Avant de commencer

- ciblez Windows 11 x64 ;
- installez le SDK .NET 10 ;
- utilisez Inno Setup 6 uniquement pour produire un installateur local ;
- ne committez jamais `bin/`, `obj/`, `artifacts/publish/` ou un fichier `.exe` généré.

## Workflow recommandé

1. Créez une branche dédiée depuis `main`.
2. Décrivez clairement le problème ou la fonctionnalité.
3. Faites les modifications les plus ciblées possible.
4. Ajoutez ou mettez à jour les tests concernés.
5. Exécutez les tests avant de pousser :

   ```powershell
   dotnet test tests/UltrawideToys.Core.Tests/UltrawideToys.Core.Tests.csproj
   ```

6. Ouvrez une pull request avec un résumé, les étapes de validation et, pour l’interface, une capture avant/après.

## Règles de code

- conservez les calculs de zones dans `UltrawideToys.Core` afin qu’ils restent testables ;
- n’ajoutez pas de logique métier directement dans le code-behind WPF ;
- respectez les textes français et anglais existants ;
- ne modifiez pas les exclusions de sécurité, l’IPC administrateur ou les hooks de fenêtres sans test et justification ;
- gardez les changements compatibles avec les configurations déjà enregistrées.

## Binaries et Releases

Les exécutables et l’installateur sont des artefacts de build. Ils doivent être attachés à une GitHub Release, jamais ajoutés à l’historique Git. Le workflow de build peut générer l’installateur et son empreinte SHA-256 à partir d’un tag `v*`.

## Licence des contributions

Toute contribution est publiée sous la licence du projet. L’usage commercial reste interdit sans autorisation écrite préalable.

