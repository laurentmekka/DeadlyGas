# Deadly Gas — fin de raid réaliste (SPT 4.0 / EFT 0.16.9)

Remplace le MIA arbitraire de fin de timer par un **gaz mortel** : quand le
temps expire, la zone se remplit d'un gaz (teinte verte + brouillard), et la
vie draine lentement — environ 3 minutes pour mourir à vie pleine (réglable).
Le joueur peut encore tenter l'extraction : mieux vaut 2 minutes de course
dans le gaz qu'un écran MIA à 2 mètres de l'extract.

Mod **client** (BepInEx), indépendant de QuestManiac-Next.

## Installation

1. `build.bat` (compile et copie la DLL dans `BepInEx\plugins\`).
2. Lancer SPT normalement.

## Configuration

En jeu via **F12** (Configuration Manager) ou dans
`BepInEx\config\com.mekka.deadlygas.cfg` :

| Option | Défaut | Effet |
|---|---|---|
| Activer | oui | Coupe/rétablit tout le comportement |
| Minutes avant la mort | 3 | Survie dans le gaz à vie pleine (calé sur le thorax) |
| Montée du gaz (secondes) | 20 | Délai avant les premiers dégâts |
| Effets visuels | oui | Teinte verte + brouillard |
| Mode sonde | non | Diagnostic (voir plus bas) |

## Comment ça marche

Patch Harmony sur `EndByTimerScenario.Update` : tant que le timer tourne,
comportement vanilla ; quand il expire, le MIA est bloqué et le gaz démarre
(dégâts Poison répartis sur tout le corps, 1 tick/seconde). La mort dans le
gaz est une mort normale (assurance, perte de stuff selon tes règles). Une
extraction réussie reste une extraction réussie.

Tous les types du jeu sont résolus **par réflexion** : si une mise à jour
d'EFT renomme quelque chose, le mod ne casse rien — il se désactive et le
signale dans le log.

## Premier test (checklist)

1. Raid Usine (timer court) — ou réduire la durée de raid pour tester vite.
2. Laisser le timer expirer SANS s'extraire :
   - à 0:00, pas d'écran MIA, l'écran verdit, le brouillard monte ;
   - après ~20 s, la vie commence à baisser sur toutes les parties ;
   - mort en ~3 min (vie pleine) → écran de mort normal.
3. Refaire un raid, s'extraire PENDANT le gaz → extraction comptée Survived.
4. Vérifier le log `BepInEx\LogOutput.log` : ligne
   `[DeadlyGas] Patch fin-de-timer appliqué`.

## Si ça ne marche pas

Activer **Mode sonde** (F12), refaire un raid jusqu'à la fin du timer, puis
envoyer `BepInEx\LogOutput.log`. Le log contient alors les noms réels des
types/membres de ta version du jeu — de quoi ajuster le mod en une itération.

## Limites connues (v0.1)

- Pas de son de toux / respiration (phase 2).
- Le masque à gaz ne protège pas encore (phase 2 — bonne idée cela dit).
- Les bots ne sont pas affectés par le gaz (les Scavs sont immunisés, canon
  discutable mais gameplay sain).
- Non testé avec Fika/coop.
