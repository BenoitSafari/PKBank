# PKBank

Éditeur de sauvegardes Pokémon multi-plateforme, bâti sur [PKHeX.Core](https://github.com/kwsch/PKHeX)
avec une interface [Avalonia](https://avaloniaui.net/).

## Projets

| Projet | Rôle |
| --- | --- |
| `PKBank.Core` | Données et logique de jeu complémentaires à `PKHeX.Core`. |
| `PKBank.Desktop` | Application de bureau Avalonia (Linux, Windows, macOS). |

`PKHeX` est consommé en sous-module Git sous `packages/PKHeX` ; les projets
`PKHeX.Core` et `PKHeX.Drawing.*` y sont référencés directement.

## Prérequis

- SDK .NET 10.0
- Git (avec les sous-modules initialisés)

## Cloner

```sh
git clone --recurse-submodules git@github.com:BenoitSafari/PKBank.git
```

Sur un dépôt déjà cloné :

```sh
git submodule update --init --recursive
```

## Construire et lancer

```sh
dotnet build PKBank.slnx
dotnet run --project PKBank.Desktop
```

## Licence

`PKHeX` est distribué sous licence GPL-3.0-or-later ; ce dépôt en dérive et suit la même licence.
